using Microsoft.AspNetCore.Http;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Data;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IdentityDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(IdentityDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RecordAsync(
        AuditEventType eventType,
        string description,
        Guid? subjectUserId = null,
        string? subjectDisplay = null,
        Guid? actorUserId = null,
        string? actorDisplay = null,
        CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EventType = eventType,
            SubjectUserId = subjectUserId,
            SubjectDisplay = subjectDisplay,
            ActorUserId = actorUserId,
            ActorDisplay = actorDisplay ?? subjectDisplay,
            Description = description,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync(ct);
    }
}
