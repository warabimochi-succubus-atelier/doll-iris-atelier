using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DollIrisGenerator.Web.Services;

/// <summary>
/// wwwroot/js/preview.js through [JSImport]: strings are passed directly (no JSON), so a redraw hands its SVG to
/// the browser without the cost of building a Base64 data URI.
/// The module is loaded once at startup (Program.cs).
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class PreviewInterop
{
    public const string ModuleName = "preview";

    /// <summary>Relative to the framework folder, so it also works when the app is served from a subfolder.</summary>
    public const string ModulePath = "../js/preview.js";

    /// <summary>A blob: URL of <paramref name="svg"/>, once the picture has loaded (shown at once when put on an &lt;img&gt;).</summary>
    [JSImport("loadSvg", ModuleName)]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> LoadSvgAsync(string svg);

    [JSImport("revoke", ModuleName)]
    internal static partial void Revoke(string url);
}
