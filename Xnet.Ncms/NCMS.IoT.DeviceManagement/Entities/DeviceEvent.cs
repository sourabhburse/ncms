namespace NCMS.IoT.DeviceManagement.Entities;

public sealed class DeviceEvent
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string EventType { get; set; }
    public string? Severity { get; set; }
    public required string PayloadJson { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
