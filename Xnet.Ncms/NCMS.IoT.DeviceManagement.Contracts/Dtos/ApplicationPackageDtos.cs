namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed record ApplicationPackageDto(
    Guid Id,
    string Name,
    List<string> Tags,
    int VersionCount,
    DateTimeOffset CreatedAt);

public sealed record ApplicationPackageVersionListItemDto(
    Guid Id,
    Guid ApplicationPackageId,
    string PackageName,
    List<string> Tags,
    string Version,
    string PackageFormat,
    bool IsEnabled,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    bool HasArtifact,
    int CompatibleProductCount,
    List<string> CompatibleProductNames);

public sealed record ApplicationPackageDependencyDto(
    Guid DependsOnApplicationPackageId,
    string DependsOnPackageName,
    string? VersionConstraint);

public sealed record ApplicationPackageVersionDetailDto(
    Guid Id,
    Guid ApplicationPackageId,
    string PackageName,
    List<string> Tags,
    string Version,
    string PackageFormat,
    string FileName,
    string StoragePath,
    long SizeBytes,
    string Sha256Checksum,
    string? Md5Checksum,
    string? Metadata,
    string? ReleaseNotes,
    bool IsEnabled,
    DateTimeOffset UploadedAt,
    string UploadedBy,
    List<Guid> CompatibleProductIds,
    List<string> CompatibleProductNames,
    List<ApplicationPackageDependencyDto> Dependencies);

public sealed record CreateApplicationPackageRequest(string Name, List<string> Tags);

public sealed record CreateApplicationPackageWithVersionRequest(
    string Name, List<string> Tags,
    string Version, string PackageFormat, string? ReleaseNotes,
    List<Guid> ProductIds);

public sealed record CreateApplicationPackageVersionRequest(
    string Version, string PackageFormat, string? ReleaseNotes,
    List<Guid> ProductIds);

public sealed record SetApplicationCompatibilityRequest(List<Guid> ProductIds);

public sealed record SetDependencyItem(Guid DependsOnApplicationPackageId, string? VersionConstraint);

public sealed record SetDependenciesRequest(List<SetDependencyItem> Dependencies);
