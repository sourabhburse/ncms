namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Logical software package identity — groups multiple <see cref="ApplicationPackageVersion"/>
/// uploads under one browsable name. Carries no business rules of its own; all lifecycle,
/// compatibility, and validation invariants live on ApplicationPackageVersion (mirrors how
/// Firmware carries its own lifecycle directly — the split here exists purely to support
/// version-history browsing and dependency declarations, which firmware never needed).
/// </summary>
public sealed class ApplicationPackage
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Free-form labels for grouping/searching packages (stored as a Postgres text[]). Replaces
    /// the former single Category — a package can now carry several tags (e.g. "Network",
    /// "Security"). Purely organisational; never consulted by compatibility or dispatch logic.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<ApplicationPackageVersion> Versions { get; set; } = new List<ApplicationPackageVersion>();
}
