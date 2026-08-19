using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed record FirmwareDto(
    Guid Id,
    string Name,
    string Version,
    string? Description,
    string ReleaseNotes,
    string DeviceTypeCode,
    string BinaryChecksum,
    string StoragePath,
    long Size,
    string? MinRequiredFirmwareVersion,
    string CreatedBy,
    DateTimeOffset UploadedAt,
    bool IsEnabled);

public sealed record FirmwarePackageListItemDto(
    Guid Id,
    string Name,
    string Version,
    string ProductModel,
    long Size,
    DateTimeOffset UploadedAt,
    bool IsEnabled);

public sealed record FirmwarePackageDetailDto(
    Guid Id,
    string Name,
    string Version,
    string FileName,
    string StoragePath,
    long Size,
    FirmwareType Type,
    string? Remark,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    bool IsEnabled,
    List<string> ProductModels,
    List<Guid> ProductIds);

public sealed record CreateFirmwareRequest(
    string Version,
    string Name,
    string? Description,
    string ReleaseNotes,
    string DeviceTypeCode,
    string? MinRequiredFirmwareVersion);

public sealed record SetCompatibilityRequest(List<Guid> VariantIds);
