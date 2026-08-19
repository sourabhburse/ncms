using System.Linq.Expressions;
using Mediator;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Pagination;
using NCMS.Persistence.Specifications;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

/// <summary>Query composition for the Config Files index: whitelisted server-side sort + filters + projection.</summary>
public sealed class ConfigProfilesSpecification : Specification<ConfigProfile, ConfigProfileListItemDto>
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<ConfigProfile, object>>> SortMap =
        new Dictionary<string, Expression<Func<ConfigProfile, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["profileName"] = p => p.ProfileName,
            ["product"] = p => p.Product.Name,
            ["status"] = p => p.Status,
            ["uploaded"] = p => p.UploadedAt,
        };

    public ConfigProfilesSpecification(string? sort, string? name = null, Guid? productId = null, ProfileStatus? status = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Where(p => p.ProfileName.Contains(name));
        if (productId is { } pid) Where(p => p.ProductId == pid);
        if (status is { } s) Where(p => p.Status == s);

        ApplySortingOverride(sort, () => OrderByDescending(p => p.UploadedAt), SortMap);
        Select(p => new ConfigProfileListItemDto(
            p.Id, p.ProfileName, p.Product.Name, p.Remark, p.Status, p.UploadedAt));
    }
}

/// <summary>Paginated config profiles for the management grid (shared pagination).</summary>
public static class ListConfigProfilesPaged
{
    public sealed record Query(
        string? Sort,
        int Page,
        int PageSize,
        string? Name = null,
        Guid? ProductId = null,
        ProfileStatus? Status = null) : IRequest<PagedResponse<ConfigProfileListItemDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<ConfigProfileListItemDto>>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<PagedResponse<ConfigProfileListItemDto>> Handle(Query q, CancellationToken ct) =>
            await _db.ConfigProfiles
                .ApplySpecification(new ConfigProfilesSpecification(q.Sort, q.Name, q.ProductId, q.Status))
                .ToPagedResponseAsync(new PagedQuery(q.Page, q.PageSize), ct);
    }
}
