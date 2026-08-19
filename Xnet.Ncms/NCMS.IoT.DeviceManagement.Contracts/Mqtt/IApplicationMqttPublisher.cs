using System.Text.Json.Serialization;

namespace NCMS.IoT.DeviceManagement.Contracts.Mqtt;

public interface IApplicationMqttPublisher
{
    Task PublishApplicationCommandAsync(Guid deviceId, IApplicationCommand command, CancellationToken ct = default);
}

/// <summary>
/// Marker for the two device-facing wire payload shapes an application task can dispatch —
/// implementations are serialized as-is (no shared envelope), so the shape on the wire is
/// exactly the record's own properties.
/// </summary>
public interface IApplicationCommand;

/// <summary>
/// The device-facing OTA install/upgrade/downgrade command. PackageUrl is opaque to the
/// server; the device agent downloads it, verifies size/checksums, and installs. Task/dispatch
/// bookkeeping (task id, retry count) stays server-side only and is deliberately NOT part of
/// this wire payload — only Action is included, since the device needs it to know what to do
/// with the downloaded package.
/// </summary>
public sealed record ApplicationInstallCommand(
    [property: JsonPropertyName("action")] string Action,
     [property: JsonPropertyName("package_name")] string PackageName,
    [property: JsonPropertyName("package_url")] string PackageUrl,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("md5")] string Md5,
    [property: JsonPropertyName("sha256")] string Sha256) : IApplicationCommand;

/// <summary>
/// The device-facing removal command. A Remove task has no package artifact to download, so
/// the payload only identifies which application to uninstall by name.
/// </summary>
public sealed record ApplicationRemoveCommand(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("package_name")] string PackageName) : IApplicationCommand;
