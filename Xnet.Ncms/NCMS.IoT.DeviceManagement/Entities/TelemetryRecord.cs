namespace NCMS.IoT.DeviceManagement.Entities;

public sealed class TelemetryRecord
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string PayloadJson { get; set; }
    public required string Topic { get; set; }
    public byte QosLevel { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
