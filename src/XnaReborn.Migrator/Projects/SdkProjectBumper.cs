using System.Text.RegularExpressions;

namespace XnaReborn.Migrator.Projects;

/// <summary>The result of bumping an SDK-style MonoGame project.</summary>
/// <param name="Text">The new project text. Equals the input when nothing needed a change.</param>
/// <param name="Notes">Things the bump could not do and a person must check.</param>
public sealed record BumpResult(string Text, IReadOnlyList<string> Notes);

/// <summary>
/// Moves an SDK-style project that is already on MonoGame 3.8.x to the pinned version: target framework, MonoGame package versions
/// and the <c>dotnet-mgcb</c> tool version. It edits the text in place, so every other byte of the project stays as the author wrote it.
/// </summary>
public static partial class SdkProjectBumper
{
    [GeneratedRegex(@"<PackageReference\b[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex PackageReferenceRegex();

    [GeneratedRegex(@"\b(Include|Update)\s*=\s*""(MonoGame\.Framework\.[A-Za-z0-9.]+|MonoGame\.Content\.Builder\.Task)""", RegexOptions.CultureInvariant)]
    private static partial Regex MonoGamePackageRegex();

    [GeneratedRegex(@"(\bVersion\s*=\s*"")[^""]*("")", RegexOptions.CultureInvariant)]
    private static partial Regex VersionAttributeRegex();

    [GeneratedRegex(@"(<TargetFramework>\s*)(netcoreapp3\.1|net5\.0|net6\.0|net7\.0)(-[A-Za-z0-9.]+)?(\s*</TargetFramework>)", RegexOptions.CultureInvariant)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"(""dotnet-mgcb""\s*:\s*\{[^}]*?""version""\s*:\s*"")[^""]*("")", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex MgcbToolRegex();

    /// <summary>Bumps the text of an SDK-style project file.</summary>
    /// <param name="projectText">The project file text.</param>
    /// <param name="versions">The pinned versions.</param>
    /// <returns>The new text and notes.</returns>
    public static BumpResult Bump(string projectText, PackageVersions versions)
    {
        ArgumentNullException.ThrowIfNull(projectText);
        ArgumentNullException.ThrowIfNull(versions);
        var notes = new List<string>();
        var windows = projectText.Contains("MonoGame.Framework.WindowsDX", StringComparison.OrdinalIgnoreCase);

        var text = PackageReferenceRegex().Replace(projectText, m =>
        {
            var element = m.Value;
            var match = MonoGamePackageRegex().Match(element);
            if (!match.Success)
            {
                return element;
            }

            if (!VersionAttributeRegex().IsMatch(element))
            {
                notes.Add($"The version of '{match.Groups[2].Value}' is not an attribute (or is central), so it was not changed; set it to {versions.MonoGame}.");
                return element;
            }

            var version = match.Groups[2].Value.StartsWith("MonoGame.Content.Builder", StringComparison.OrdinalIgnoreCase) ? versions.ContentBuilder : versions.MonoGame;
            return VersionAttributeRegex().Replace(element, "${1}" + version + "${2}");
        });

        var framework = windows ? versions.WindowsTargetFramework : versions.TargetFramework;
        text = TargetFrameworkRegex().Replace(text, m => m.Groups[1].Value + framework + m.Groups[4].Value);
        if (text.Contains("<TargetFrameworks>", StringComparison.Ordinal))
        {
            notes.Add($"The project uses TargetFrameworks, which was not changed; set it to {framework}.");
        }

        return new BumpResult(text, notes);
    }

    /// <summary>Sets the <c>dotnet-mgcb</c> version in the text of a tool manifest and keeps every other byte.</summary>
    /// <param name="manifestText">The <c>dotnet-tools.json</c> text.</param>
    /// <param name="version">The pinned <c>dotnet-mgcb</c> version.</param>
    /// <returns>The new text, or the same text when the manifest has no <c>dotnet-mgcb</c> entry.</returns>
    public static string BumpToolManifest(string manifestText, string version)
    {
        ArgumentNullException.ThrowIfNull(manifestText);
        return MgcbToolRegex().Replace(manifestText, "${1}" + version + "${2}");
    }
}
