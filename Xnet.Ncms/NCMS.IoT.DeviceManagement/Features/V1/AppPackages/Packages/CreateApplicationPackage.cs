using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

public static class CreateApplicationPackage
{
    public sealed record Command(string Name, List<string> Tags) : IRequest<ApplicationPackageDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50);
        }
    }

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ApplicationPackageDto> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = new ApplicationPackage
            {
                Id = Guid.NewGuid(),
                Name = cmd.Name.Trim(),
                Tags = TagNormalizer.Normalize(cmd.Tags),
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.ApplicationPackages.Add(pkg);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                throw new InvalidOperationException($"A application package named '{cmd.Name}' already exists.");
            }

            return new ApplicationPackageDto(pkg.Id, pkg.Name, pkg.Tags, 0, pkg.CreatedAt);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateApplicationPackageRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(req.Name, req.Tags), ct);
                return Results.Created($"/api/v1/application/packages/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Add)
        .WithSummary("Create a new application package (logical identity)");
}
