namespace NCMS.IoT.DeviceManagement.Configuration;

/// <summary>
/// Storage + download settings for the files this platform distributes to devices —
/// firmware binaries and configuration files. Files are saved to <see cref="StoragePath"/>
/// (the physical path is what's persisted in the database), and devices download them from
/// <c>{PublicBaseUrl}/{segment}/{file}</c>.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Local disk directory where uploaded firmware AND config files are saved.</summary>
    public string StoragePath { get; set; } = "files";

    /// <summary>Device-reachable base URL that serves the stored files.</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:7206";

    /// <summary>URL path segment under which firmware files are served to devices.</summary>
    public string FirmwareUrlSegment { get; set; } = "firmware-files";

    /// <summary>URL path segment under which config files are served to devices.</summary>
    public string ConfigUrlSegment { get; set; } = "config-files";

    /// <summary>URL path segment under which application package files are served to devices.</summary>
    public string ApplicationUrlSegment { get; set; } = "application-files";

    /// <summary>Device-facing download URL for a firmware file, from its physical path.</summary>
    public string BuildFirmwareUrl(string physicalPath) => BuildPublicUrl(physicalPath, FirmwareUrlSegment);

    /// <summary>Device-facing download URL for a config file, from its physical path.</summary>
    public string BuildConfigUrl(string physicalPath) => BuildPublicUrl(physicalPath, ConfigUrlSegment);

    /// <summary>Device-facing download URL for an application package file, from its physical path.</summary>
    public string BuildApplicationUrl(string physicalPath) => BuildPublicUrl(physicalPath, ApplicationUrlSegment);

    /// <summary>
    /// Builds the device-facing download URL for a stored file from its physical path.
    /// The database holds the physical path (so it matches where the file actually lives);
    /// this derives the public URL on demand, so it always reflects the current
    /// <see cref="PublicBaseUrl"/> / segment config even if it changes after upload.
    /// </summary>
    private string BuildPublicUrl(string physicalPath, string segment) =>
        $"{PublicBaseUrl.TrimEnd('/')}/{segment.Trim('/')}/{Path.GetFileName(physicalPath)}";
}
