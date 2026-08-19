namespace NCMS.IoT.Host.Application.Abstractions;

public interface INavigationMenuProvider
{
    IReadOnlyList<NavigationMenuItem> GetItems();
}

/// <summary>
/// A sidebar navigation entry. Two shapes:
///   • a leaf link — has a <see cref="Page"/> (and optional <see cref="ActivePrefix"/>);
///   • a parent group — has <see cref="Children"/> and acts as a dropdown toggle that
///     expands/collapses them (its <see cref="Page"/> is empty).
/// </summary>
public sealed record NavigationMenuItem(
    string Title,
    string Page,
    string Icon,
    string? ActivePrefix = null,
    IReadOnlyList<NavigationMenuItem>? Children = null,
    string? RequiredPermission = null)
{
    /// <summary>True when this item is a collapsible parent rather than a direct link.</summary>
    public bool HasChildren => Children is { Count: > 0 };

    /// <summary>Creates a collapsible parent group whose children render under a dropdown toggle.</summary>
    public static NavigationMenuItem Group(string title, string icon, params NavigationMenuItem[] children) =>
        new(title, "", icon, null, children);
}
