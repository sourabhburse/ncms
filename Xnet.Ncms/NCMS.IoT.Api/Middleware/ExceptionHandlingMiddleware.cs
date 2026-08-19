using System.Net;
using System.Text.Json;
using FluentValidation;
using NCMS.IoT.Core.Domain.Exceptions;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Api.Middleware;

/// <summary>
/// Translates domain exceptions to structured HTTP problem responses.
/// Prevents internal stack traces from leaking to clients.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (exception is ValidationException validationEx)
        {
            context.Response.StatusCode = 422;
            context.Response.ContentType = "application/json";
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            var validationBody = JsonSerializer.Serialize(new
            {
                error = "VALIDATION_FAILED",
                message = "One or more validation errors occurred.",
                errors,
                traceId = context.TraceIdentifier
            }, SerializerOptions);
            await context.Response.WriteAsync(validationBody);
            return;
        }

        var (statusCode, errorCode, message) = exception switch
        {
            HardwareNotFoundException e =>
                (HttpStatusCode.NotFound, "HARDWARE_NOT_FOUND", e.Message),

            DeviceAlreadyProvisionedException e =>
                (HttpStatusCode.Conflict, "ALREADY_PROVISIONED", e.Message),

            DeviceKeyMismatchException e =>
                (HttpStatusCode.Conflict, "KEY_MISMATCH", e.Message),

            InvalidBootstrapTokenException e =>
                (HttpStatusCode.Unauthorized, "INVALID_TOKEN", e.Message),

            PkiException e =>
                (HttpStatusCode.InternalServerError, "PKI_ERROR", "Certificate issuance failed."),

            NotFoundException e =>
                (HttpStatusCode.NotFound, "NOT_FOUND", e.Message),

            ConflictException e =>
                (HttpStatusCode.Conflict, "CONFLICT", e.Message),

            DomainException e =>
                (HttpStatusCode.BadRequest, "DOMAIN_ERROR", e.Message),

            KeyNotFoundException e =>
                (HttpStatusCode.NotFound, "NOT_FOUND", e.Message),

            InvalidOperationException e =>
                (HttpStatusCode.BadRequest, "INVALID_OPERATION", e.Message),

            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning("Domain exception: {Code} — {Message}", errorCode, message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            error = errorCode,
            message,
            traceId = context.TraceIdentifier
        }, SerializerOptions);

        await context.Response.WriteAsync(body);
    }
}
