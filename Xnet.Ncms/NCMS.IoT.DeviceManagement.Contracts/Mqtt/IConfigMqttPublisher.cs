namespace NCMS.IoT.DeviceManagement.Contracts.Mqtt;

public interface IConfigMqttPublisher
{
    Task PublishConfigCommandAsync(Guid deviceId, ConfigCommandMessage command, CancellationToken ct = default);
}

public sealed record ConfigCommandMessage(
    string CommandId,
    string TaskDeviceId,
    string TaskId,
    string ProfileName,
    string ConfigUrl,
    long Size,
    string Md5,
    DateTime ExpiresAt);
