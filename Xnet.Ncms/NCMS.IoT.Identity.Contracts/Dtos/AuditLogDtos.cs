using NCMS.IoT.Identity.Contracts.Enums;

namespace NCMS.IoT.Identity.Contracts.Dtos;

public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditEventType EventType,
    Guid? SubjectUserId,
    string? SubjectDisplay,
    Guid? ActorUserId,
    string? ActorDisplay,
    string Description,
    string? IpAddress);
