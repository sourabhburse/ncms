using NCMS.IoT.Identity.Contracts.Enums;

namespace NCMS.IoT.Identity.Services;

public interface IAuditLogService
{
    Task RecordAsync(
        AuditEventType eventType,
        string description,
        Guid? subjectUserId = null,
        string? subjectDisplay = null,
        Guid? actorUserId = null,
        string? actorDisplay = null,
        CancellationToken ct = default);
}
