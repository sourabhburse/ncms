namespace NCMS.IoT.DeviceManagement.Contracts.Mqtt;

public interface IFirmwareMqttPublisher
{
    Task PublishFirmwareCommandAsync(Guid deviceId, FirmwareCommandMessage command, CancellationToken ct = default);
}

public sealed record FirmwareCommandMessage(
    string CommandId,
    string TaskDeviceId,
    string TaskId,
    string TargetVersion,
    string PackageUrl,
    long Size,
    string Md5,
    string Sha256,
    bool RollbackOnFailure,
    DateTime ExpiresAt);
