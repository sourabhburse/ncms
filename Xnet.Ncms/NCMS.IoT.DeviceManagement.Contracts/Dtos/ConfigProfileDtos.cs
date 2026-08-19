using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed record ConfigProfileDto(
    Guid Id,
    string ProfileName,
    string FileName,
    string StoragePath,
    long Size,
    Guid ProductId,
    string ProductName,
    ProfileStatus Status,
    string? Remark,
    string CreatedBy,
    DateTimeOffset UploadedAt);

public sealed record ConfigProfileListItemDto(
    Guid Id,
    string ProfileName,
    string ProductModel,
    string? Remark,
    ProfileStatus Status,
    DateTimeOffset UploadedAt);

public sealed record ConfigProfileDetailDto(
    Guid Id,
    string ProfileName,
    string FileName,
    string StoragePath,
    long Size,
    string ProductModel,
    Guid ProductId,
    ProfileStatus Status,
    string? Remark,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record CreateConfigProfileRequest(
    string ProfileName,
    Guid ProductId,
    string? Remark);
