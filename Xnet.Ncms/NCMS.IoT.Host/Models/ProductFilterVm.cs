using NCMS.IoT.DeviceManagement.Contracts.Dtos;

namespace NCMS.IoT.Host.Models;

/// <summary>
/// View model for the shared <c>_ProductFilters</c> partial — the cascading
/// Series → Type → Model dropdowns. <see cref="Options"/> is the full hierarchy; the
/// selected ids reflect the currently-applied filters so they survive round-trips.
/// </summary>
public sealed class ProductFilterVm
{
    public required ProductFilterOptionsDto Options { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? TypeId { get; init; }
    public Guid? ProductId { get; init; }
}
