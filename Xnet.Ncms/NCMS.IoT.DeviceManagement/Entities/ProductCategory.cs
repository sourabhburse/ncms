using NCMS.Shared.Domain;

namespace NCMS.IoT.DeviceManagement.Entities;

public sealed class ProductCategory : BaseEntity<Guid>
{
    private ProductCategory() { }

    public string Name { get; private set; } = default!;
    public bool IsDeleted { get; private set; }

    public ICollection<ProductType> ProductTypes { get; } = new List<ProductType>();

    public static ProductCategory Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100) throw new ArgumentException("Name must be 100 characters or fewer.", nameof(name));

        return new ProductCategory { Id = Guid.NewGuid(), Name = name.Trim() };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100) throw new ArgumentException("Name must be 100 characters or fewer.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>Call only after verifying no ProductTypes exist.</summary>
    public void SoftDelete() => IsDeleted = true;
}
