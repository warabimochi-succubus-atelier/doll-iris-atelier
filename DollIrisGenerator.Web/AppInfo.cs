using System.Reflection;

namespace DollIrisGenerator.Web;

/// <summary>
/// The app's name, version, author and links, shared by the header and the 「このアプリについて」 dialog.
/// Same values as the desktop app's AppInfo (DollIrisGenerator/AppInfo.cs).
/// </summary>
public static class AppInfo
{
    public const string Name = "Doll Iris Atelier";
    public const string Author = "わらびもちサキュバス工房";
    public const string Copyright = "© 2026 わらびもちサキュバス工房";
    public const string License = "MIT License (ロゴとアイコンを除く)";

    public const string RepositoryUrl = "https://github.com/warabimochi-succubus-atelier/doll-iris-atelier";
    public const string ManualUrl = RepositoryUrl + "/blob/main/USER_MANUAL.md";
    public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";

    /// <summary>"v0.11.0", from &lt;Version&gt; in the project file (the part before any "+commit").</summary>
    public static readonly string VersionLabel = "v" + (typeof(AppInfo).Assembly
        .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
        .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion
        .Split('+')[0] ?? "?");
}
