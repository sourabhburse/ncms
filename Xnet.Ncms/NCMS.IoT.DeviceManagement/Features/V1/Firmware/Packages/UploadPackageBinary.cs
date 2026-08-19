using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

public static class UploadPackageBinary
{
    public sealed record Command(Guid PackageId, IFormFile File) : IRequest<FirmwareDto>;

    public sealed class Handler : IRequestHandler<Command, FirmwareDto>
    {
        private readonly DeviceManagementDbContext _db;
        private readonly FileStorageOptions _opts;

        public Handler(DeviceManagementDbContext db, IOptions<FileStorageOptions> opts)
        {
            _db = db;
            _opts = opts.Value;
        }

        public async ValueTask<FirmwareDto> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = await _db.Firmwares.FindAsync([cmd.PackageId], ct)
                ?? throw new KeyNotFoundException($"Package {cmd.PackageId} not found");

            Directory.CreateDirectory(_opts.StoragePath);
            var filePath = Path.Combine(_opts.StoragePath, $"{cmd.PackageId}_{cmd.File.FileName}");

            await using (var fileStream = File.Create(filePath))
            {
                await using var uploadStream = cmd.File.OpenReadStream();
                await uploadStream.CopyToAsync(fileStream, ct);
            }

            var fileInfo = new FileInfo(filePath);
            pkg.FileName = cmd.File.FileName;
            pkg.Size = fileInfo.Length;
            pkg.BinaryChecksum = await ComputeSha256Async(filePath, ct);
            pkg.Md5 = await ComputeMd5Async(filePath, ct);
            // Store the actual physical location on disk; the device download URL is derived
            // from PublicBaseUrl at dispatch time (see FileStorageOptions.BuildPublicUrl).
            pkg.StoragePath = filePath;
            await _db.SaveChangesAsync(ct);
            return ListPackages.Handler.ToDto(pkg);
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
        group.MapPost("/{id:guid}/binary", async (Guid id, IFormFile file, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Command(id, file), ct)))
        .DisableAntiforgery()
        .RequireAuthorization(DeviceManagementPermissions.Packages.Upload)
        .WithSummary("Upload firmware binary file")
        .WithRequestTimeout(TimeSpan.FromMinutes(10));
}
