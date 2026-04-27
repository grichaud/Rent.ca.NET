using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Rent.Web.Features.Shared;

[HtmlTargetElement("icon", Attributes = "name")]
public sealed class IconTagHelper : TagHelper
{
    public string Name { get; set; } = string.Empty;
    public string? Class { get; set; }
    public int StrokeWidth { get; set; } = 2;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!IconPaths.TryGetValue(Name, out var path))
        {
            output.SuppressOutput();
            return;
        }

        var cssClass = string.IsNullOrWhiteSpace(Class) ? "h-5 w-5" : Class;
        var encodedClass = HtmlEncoder.Default.Encode(cssClass);

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("xmlns", "http://www.w3.org/2000/svg");
        output.Attributes.SetAttribute("viewBox", "0 0 24 24");
        output.Attributes.SetAttribute("fill", "none");
        output.Attributes.SetAttribute("stroke", "currentColor");
        output.Attributes.SetAttribute("stroke-width", StrokeWidth.ToString());
        output.Attributes.SetAttribute("stroke-linecap", "round");
        output.Attributes.SetAttribute("stroke-linejoin", "round");
        output.Attributes.SetAttribute("class", encodedClass);
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Content.SetHtmlContent(path);
    }

    // Lucide icons (MIT) — paths copied verbatim from lucide.dev.
    // viewBox 0 0 24, stroke 2, round caps/joins.
    private static readonly Dictionary<string, string> IconPaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["search"]       = "<circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/>",
        ["map-pin"]      = "<path d='M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0'/><circle cx='12' cy='10' r='3'/>",
        ["heart"]        = "<path d='M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.29 1.51 4.04 3 5.5l7 7Z'/>",
        ["dog"]          = "<path d='M11.25 16.25h1.5L12 17z'/><path d='M16 14v.5'/><path d='M4.42 11.247A13.152 13.152 0 0 0 4 14.556C4 18.728 7.582 21 12 21s8-2.272 8-6.444a11.702 11.702 0 0 0-.493-3.309'/><path d='M8 14v.5'/><path d='M8.5 8.5c-.384 1.05-1.083 2.028-2.344 2.5-1.931.722-3.576-.297-3.656-1-.113-.994 1.177-6.53 4-7 1.923-.321 3.651.845 3.651 2.235A7.497 7.497 0 0 1 14 5.277c0-1.39 1.844-2.598 3.767-2.277 2.823.47 4.113 6.006 4 7-.08.703-1.725 1.722-3.656 1-1.261-.472-1.96-1.45-2.344-2.5'/>",
        ["sofa"]         = "<path d='M20 9V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v3'/><path d='M2 11v5a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-5a2 2 0 0 0-4 0v2H6v-2a2 2 0 0 0-4 0Z'/><path d='M4 18v2'/><path d='M20 18v2'/><path d='M12 4v9'/>",
        ["sparkles"]     = "<path d='M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z'/><path d='M20 3v4'/><path d='M22 5h-4'/><path d='M4 17v2'/><path d='M5 18H3'/>",
        ["languages"]    = "<path d='m5 8 6 6'/><path d='m4 14 6-6 2-3'/><path d='M2 5h12'/><path d='M7 2h1'/><path d='m22 22-5-10-5 10'/><path d='M14 18h6'/>",
        ["log-in"]       = "<path d='M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4'/><polyline points='10 17 15 12 10 7'/><line x1='15' x2='3' y1='12' y2='12'/>",
        ["log-out"]      = "<path d='M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4'/><polyline points='16 17 21 12 16 7'/><line x1='21' x2='9' y1='12' y2='12'/>",
        ["user-plus"]    = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><line x1='19' x2='19' y1='8' y2='14'/><line x1='22' x2='16' y1='11' y2='11'/>",
        ["user"]         = "<path d='M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2'/><circle cx='12' cy='7' r='4'/>",
        ["menu"]         = "<line x1='4' x2='20' y1='12' y2='12'/><line x1='4' x2='20' y1='6' y2='6'/><line x1='4' x2='20' y1='18' y2='18'/>",
        ["x"]            = "<path d='M18 6 6 18'/><path d='m6 6 12 12'/>",
        ["sun"]          = "<circle cx='12' cy='12' r='4'/><path d='M12 2v2'/><path d='M12 20v2'/><path d='m4.93 4.93 1.41 1.41'/><path d='m17.66 17.66 1.41 1.41'/><path d='M2 12h2'/><path d='M20 12h2'/><path d='m6.34 17.66-1.41 1.41'/><path d='m19.07 4.93-1.41 1.41'/>",
        ["moon"]         = "<path d='M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z'/>",
        ["bed"]          = "<path d='M2 4v16'/><path d='M2 8h18a2 2 0 0 1 2 2v10'/><path d='M2 17h20'/><path d='M6 8v9'/>",
        ["bath"]         = "<path d='M9 6 6.5 3.5a1.5 1.5 0 0 0-1-.5C4.683 3 4 3.683 4 4.5V17a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-5'/><line x1='10' x2='8' y1='5' y2='7'/><line x1='2' x2='22' y1='12' y2='12'/><line x1='7' x2='7' y1='19' y2='21'/><line x1='17' x2='17' y1='19' y2='21'/>",
        ["home"]         = "<path d='m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'/><polyline points='9 22 9 12 15 12 15 22'/>",
        ["chevron-down"] = "<path d='m6 9 6 6 6-6'/>",
        ["mail"]         = "<rect width='20' height='16' x='2' y='4' rx='2'/><path d='m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7'/>",
        ["check"]        = "<path d='M20 6 9 17l-5-5'/>",
        ["building"]     = "<rect width='16' height='20' x='4' y='2' rx='2' ry='2'/><path d='M9 22v-4h6v4'/><path d='M8 6h.01'/><path d='M16 6h.01'/><path d='M12 6h.01'/><path d='M12 10h.01'/><path d='M12 14h.01'/><path d='M16 10h.01'/><path d='M16 14h.01'/><path d='M8 10h.01'/><path d='M8 14h.01'/>",
        ["tag"]          = "<path d='M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z'/><circle cx='7.5' cy='7.5' r='.5' fill='currentColor'/>",
        ["lock"]         = "<rect width='18' height='11' x='3' y='11' rx='2' ry='2'/><path d='M7 11V7a5 5 0 0 1 10 0v4'/>",
    };
}
