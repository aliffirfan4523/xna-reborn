using XnaReborn.Core.Models;
using XnaReborn.Migrator.Upgrade;

namespace XnaReborn.App.ViewModels;

/// <summary>One project of the report as the "Projects" list shows it: where it is, what family, which version and which platform. Display only.</summary>
public sealed class ProjectRowItem
{
    /// <summary>Creates the row text from what the analyzer learned.</summary>
    /// <param name="info">The project as analyzed.</param>
    public ProjectRowItem(ProjectInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Path = info.Path;
        (FamilyText, VersionText, PlatformText) = info.Family switch
        {
            ProjectFamily.MonoGameLegacy => ("MonoGame (old)", MonoGameVersionOf(info), MigrationScope.Describe(info.MonoGamePlatform)),
            ProjectFamily.MonoGameCurrent => ("MonoGame (current)", MonoGameVersionOf(info), MigrationScope.Describe(info.MonoGamePlatform)),
            ProjectFamily.SharpDxToolkit => ("SharpDX Toolkit", "Toolkit 2.x", ".NET Framework"),
            _ => ("XNA", info.XnaVersion == XnaVersion.Unknown ? "Version unknown" : "XNA " + XnaVersionText(info.XnaVersion), info.Platform.ToString()),
        };
        IsNotMigrated = info.Family == ProjectFamily.MonoGameLegacy && MigrationScope.IsPlatformProject(info.MonoGamePlatform);
        AutomationName = $"{Path}, {FamilyText}, {VersionText}, {PlatformText}" + (IsNotMigrated ? ", not migrated, see manual work" : string.Empty);
    }

    /// <summary>Relative path of the project file.</summary>
    public string Path { get; }

    /// <summary>"XNA", "SharpDX Toolkit", "MonoGame (old)" or "MonoGame (current)".</summary>
    public string FamilyText { get; }

    /// <summary>The version, with how sure the analyzer is for MonoGame projects, for example "3.7.0.1708 (High)".</summary>
    public string VersionText { get; }

    /// <summary>The platform in words.</summary>
    public string PlatformText { get; }

    /// <summary>True for a platform project that the migrator copies as manual work instead of migrating.</summary>
    public bool IsNotMigrated { get; }

    /// <summary>The text shown for a project that is not migrated.</summary>
    public string NotMigratedText => "Not migrated, see manual work";

    /// <summary>The line under the path: family, version and platform.</summary>
    public string Summary => $"{FamilyText} · {VersionText} · {PlatformText}";

    /// <summary>The name a screen reader reads for the row.</summary>
    public string AutomationName { get; }

    private static string MonoGameVersionOf(ProjectInfo info) => info.MonoGameVersion switch
    {
        null => "Version unknown",
        { Exact: { Length: > 0 } exact } v => $"{exact} ({v.Confidence})",
        { Band: MonoGameBand.Unknown } => "Version unknown",
        var v => $"{BandText(v.Band)} ({v.Confidence})",
    };

    private static string BandText(MonoGameBand band) => band switch
    {
        MonoGameBand.V3Early => "3.0 to 3.4",
        MonoGameBand.V35 => "3.5",
        MonoGameBand.V36 => "3.6",
        MonoGameBand.V37 => "3.7",
        MonoGameBand.V38 => "3.8",
        MonoGameBand.V32To36 => "3.2 to 3.6",
        MonoGameBand.V33To381 => "3.3 to 3.8.1",
        MonoGameBand.V35Plus => "3.5 or later",
        MonoGameBand.V380Era => "3.8.0 era",
        _ => "unknown",
    };

    private static string XnaVersionText(XnaVersion version) => version switch
    {
        XnaVersion.V10 => "1.0",
        XnaVersion.V20 => "2.0",
        XnaVersion.V30 => "3.0",
        XnaVersion.V31 => "3.1",
        XnaVersion.V40 => "4.0",
        _ => version.ToString(),
    };
}
