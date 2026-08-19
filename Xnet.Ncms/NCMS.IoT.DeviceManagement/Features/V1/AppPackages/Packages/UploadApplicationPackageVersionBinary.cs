using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;

public static class UploadApplicationPackageVersionBinary
{
    public sealed record Command(Guid VersionId, IFormFile File) : IRequest<ApplicationPackageVersionDetailDto>;

    public sealed class Handler : IRequestHandler<Command, ApplicationPackageVersionDetailDto>
    {
        private readonly DeviceManagementDbContext _db;
        private readonly FileStorageOptions _opts;

        public Handler(DeviceManagementDbContext db, IOptions<FileStorageOptions> opts)
        {
            _db = db;
            _opts = opts.Value;
        }

        public async ValueTask<ApplicationPackageVersionDetailDto> Handle(Command cmd, CancellationToken ct)
        {
            var version = await _db.ApplicationPackageVersions.FindAsync([cmd.VersionId], ct)
                ?? throw new KeyNotFoundException($"Application package version {cmd.VersionId} not found.");

            // No enabled-state gate — mirrors Firmware's UploadPackageBinary, which always
            // allows a (re-)upload regardless of IsEnabled.
            Directory.CreateDirectory(_opts.StoragePath);
            var filePath = Path.Combine(_opts.StoragePath, $"{cmd.VersionId}_{cmd.File.FileName}");

            await using (var fileStream = File.Create(filePath))
            {
                await using var uploadStream = cmd.File.OpenReadStream();
                await uploadStream.CopyToAsync(fileStream, ct);
            }

            var fileInfo = new FileInfo(filePath);
            version.FileName = cmd.File.FileName;
            version.SizeBytes = fileInfo.Length;
            version.Sha256Checksum = await ComputeSha256Async(filePath, ct);
            version.Md5Checksum = await ComputeMd5Async(filePath, ct);
            // Physical location on disk; the device download URL is derived from
            // PublicBaseUrl at dispatch time (see FileStorageOptions.BuildApplicationUrl).
            version.StoragePath = filePath;

            await _db.SaveChangesAsync(ct);

            var detail = await new GetApplicationPackageVersionDetail.Handler(_db)
                .Handle(new GetApplicationPackageVersionDetail.Query(version.Id), ct);
            return detail!;
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static async Task<string> ComputeMd5Async(string filePath, CancellationToken ct)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            await using var stream = File.OpenRead(filePath);
            var hash = await md5.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/versions/{id:guid}/binary", async (Guid id, IFormFile file, ISender sender, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await sender.Send(new Command(id, file), ct));
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .DisableAntiforgery()
        .RequireAuthorization(DeviceManagementPermissions.ApplicationPackages.Upload)
        .WithSummary("Upload the artifact file for a application package version")
        .WithRequestTimeout(TimeSpan.FromMinutes(10));
}
