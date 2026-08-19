using NCMS.Shared.Domain;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Second-level catalog grouping within a ProductCategory (e.g. "Routers" within "Network Equipment").
/// Name must be unique within the same ProductCategory.
/// </summary>
public sealed class ProductType : BaseEntity<Guid>
{
    private ProductType() { }

    public string Name { get; private set; } = default!;
    public Guid ProductCategoryId { get; private set; }
    public bool IsDeleted { get; private set; }

    public ProductCategory ProductCategory { get; private set; } = default!;
    public ICollection<Product> Products { get; } = new List<Product>();

    public static ProductType Create(string name, Guid productCategoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100) throw new ArgumentException("Name must be 100 characters or fewer.", nameof(name));
        if (productCategoryId == Guid.Empty) throw new ArgumentException("ProductCategoryId is required.", nameof(productCategoryId));

        return new ProductType
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ProductCategoryId = productCategoryId
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100) throw new ArgumentException("Name must be 100 characters or fewer.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>Call only after verifying no Products reference this type.</summary>
    public void SoftDelete() => IsDeleted = true;
}
