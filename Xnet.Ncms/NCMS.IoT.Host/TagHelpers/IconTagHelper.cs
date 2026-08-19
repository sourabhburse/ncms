using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace NCMS.IoT.Host.TagHelpers;

/// <summary>
/// Inlines an SVG icon from <c>wwwroot/icons/{name}.svg</c>:
/// <code>&lt;icon name="box" /&gt;</code>
/// Inlining (rather than &lt;img&gt;) lets the icon inherit <c>currentColor</c> so it
/// adopts the surrounding text colour. File contents are cached after first read.
/// If the named file is missing, a Tabler-Icons webfont glyph (<c>ti ti-{name}</c>)
/// is emitted as a fallback so the menu never renders an empty slot.
/// </summary>
[HtmlTargetElement("icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper : TagHelper
{
    private static readonly ConcurrentDictionary<string, string?> Cache = new();
    private readonly IWebHostEnvironment _env;

    public IconTagHelper(IWebHostEnvironment env) => _env = env;

    /// <summary>File name (without extension) under wwwroot/icons.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Extra CSS classes applied to the rendered icon.</summary>
    public string? Class { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var svg = Cache.GetOrAdd(Name, ReadSvg);

        if (svg is null)
        {
            // Fallback to the Tabler-Icons webfont glyph.
            output.TagName = "i";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Attributes.SetAttribute("class", $"ti ti-{Name} {Class}".Trim());
            return;
        }

        output.TagName = null; // render raw SVG without a wrapper element
        output.TagMode = TagMode.StartTagAndEndTag;

        // Tabler SVGs already carry class="icon ...". Merge any extra classes into
        // that existing attribute rather than emitting a second class attribute.
        var html = string.IsNullOrWhiteSpace(Class)
            ? svg
            : svg.Replace("class=\"icon ", $"class=\"icon {Class} ", StringComparison.Ordinal);

        output.Content.SetHtmlContent(html);
    }

    private string? ReadSvg(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var path = Path.Combine(root, "icons", name + ".svg");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
