using Microsoft.AspNetCore.Razor.TagHelpers;
using NCMS.IoT.Identity.Contracts.Services;

namespace NCMS.IoT.Host.TagHelpers;

/// <summary>
/// UI-level permission gate: <c>&lt;button asp-permission="@Perm.Devices.Add"&gt;...&lt;/button&gt;</c>
/// removes the element entirely when the signed-in user lacks the permission, and is a no-op
/// (renders normally) when omitted. This only controls visibility — every mutating handler
/// still enforces the same permission server-side via <c>AppPageModel.EnsurePermission</c>,
/// so hiding an action here is a UX nicety, not the authorization boundary.
///
/// Accepts several permissions separated by commas/spaces — the element renders if the user
/// has ANY of them (e.g. a "Save" button that covers both Add and Edit).
/// </summary>
[HtmlTargetElement(Attributes = "asp-permission")]
public sealed class PermissionTagHelper : TagHelper
{
    private readonly ICurrentUser _currentUser;

    public PermissionTagHelper(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>One permission, or several separated by commas/spaces — element shows if ANY match.</summary>
    public string AspPermission { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("asp-permission");

        var permissions = AspPermission.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (permissions.Length == 0)
            return;

        if (!permissions.Any(_currentUser.HasPermission))
            output.SuppressOutput();
    }
}
