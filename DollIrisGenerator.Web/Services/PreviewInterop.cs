using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DollIrisGenerator.Web.Services;

/// <summary>
/// wwwroot/js/preview.js through [JSImport]: strings are passed directly (no JSON), and the call is synchronous,
/// so a redraw hands its SVG to the browser without the cost of building a Base64 data URI.
/// The module is loaded once at startup (Program.cs).
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class PreviewInterop
{
    public const string ModuleName = "preview";

    /// <summary>Relative to the framework folder, so it also works when the app is served from a subfolder.</summary>
    public const string ModulePath = "../js/preview.js";

    [JSImport("svgUrl", ModuleName)]
    internal static partial string SvgUrl(string svg);

    [JSImport("revoke", ModuleName)]
    internal static partial void Revoke(string url);
}
