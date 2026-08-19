using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

public static class UploadProfileFile
{
    public sealed record Command(Guid ProfileId, IFormFile File) : IRequest<ConfigProfileDto>;

    public sealed class Handler : IRequestHandler<Command, ConfigProfileDto>
    {
        private readonly DeviceManagementDbContext _db;
        private readonly FileStorageOptions _opts;

        public Handler(DeviceManagementDbContext db, IOptions<FileStorageOptions> opts)
        {
            _db = db;
            _opts = opts.Value;
        }

        public async ValueTask<ConfigProfileDto> Handle(Command cmd, CancellationToken ct)
        {
            var profile = await _db.ConfigProfiles.FindAsync([cmd.ProfileId], ct)
                ?? throw new KeyNotFoundException($"Config profile {cmd.ProfileId} not found");

            Directory.CreateDirectory(_opts.StoragePath);
            var filePath = Path.Combine(_opts.StoragePath, $"{cmd.ProfileId}_{cmd.File.FileName}");

            await using (var fileStream = File.Create(filePath))
            {
                await using var uploadStream = cmd.File.OpenReadStream();
                await uploadStream.CopyToAsync(fileStream, ct);
            }

            var fileInfo = new FileInfo(filePath);
            profile.Size = fileInfo.Length;
            profile.Md5 = await ComputeMd5Async(filePath, ct);
            profile.OriginalFilename = cmd.File.FileName;
            // Store the actual physical location on disk; the device download URL is derived
            // from PublicBaseUrl at dispatch time (see FileStorageOptions.BuildPublicUrl).
            profile.StoragePath = filePath;
            await _db.SaveChangesAsync(ct);
            return ListProfiles.Handler.ToDto(profile);
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
        group.MapPost("/{id:guid}/file", async (Guid id, IFormFile file, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Command(id, file), ct)))
        .DisableAntiforgery()
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.Upload)
        .WithSummary("Upload config profile file")
        .WithRequestTimeout(TimeSpan.FromMinutes(10));
}
