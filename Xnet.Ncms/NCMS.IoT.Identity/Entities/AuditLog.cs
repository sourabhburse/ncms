using NCMS.IoT.Identity.Contracts.Enums;

namespace NCMS.IoT.Identity.Entities;

/// <summary>
/// A single security/activity event: logins (success/failure), logouts, password changes,
/// and user/role management actions. Snapshots the subject/actor display name at the time
/// of the event so history remains readable even after a user is later renamed or deleted.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public AuditEventType EventType { get; set; }

    /// <summary>The user the event is about (e.g. the account that logged in, or was edited).</summary>
    public Guid? SubjectUserId { get; set; }
    public string? SubjectDisplay { get; set; }

    /// <summary>Who performed the action — equals the subject for self-service events (login/logout/own password change).</summary>
    public Guid? ActorUserId { get; set; }
    public string? ActorDisplay { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
