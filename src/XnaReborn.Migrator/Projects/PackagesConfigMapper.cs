using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Projects;

/// <summary>A <c>packages.config</c> entry that is not carried over, with the reason.</summary>
/// <param name="Id">NuGet package id.</param>
/// <param name="Version">Version as written.</param>
/// <param name="Reason">Why the entry is not a <c>PackageReference</c> in the migrated project.</param>
public sealed record UnmappedPackage(string Id, string Version, string Reason);

/// <summary>The result of mapping a <c>packages.config</c>.</summary>
/// <param name="Mapped">Entries that become <c>PackageReference</c> items with the same id and version.</param>
/// <param name="Dropped">Entries left out on purpose (the new MonoGame package brings them).</param>
/// <param name="Manual">Entries that cannot be mapped and need a person.</param>
public sealed record PackageMapping(
    IReadOnlyList<PackageEntry> Mapped,
    IReadOnlyList<UnmappedPackage> Dropped,
    IReadOnlyList<UnmappedPackage> Manual);

/// <summary>Turns <c>packages.config</c> entries into <c>PackageReference</c> candidates. Pure: no network, no disk.</summary>
public static class PackagesConfigMapper
{
    // Target framework monikers of Windows 8, Windows Phone, UWP and Silverlight packages. They have no net8.0 asset.
    private static readonly string[] _unsupportedMonikers =
        ["win", "netcore", "wp", "wpa", "uap", "sl", "windows8", "windowsphone"];

    /// <summary>Maps the entries of one <c>packages.config</c>.</summary>
    /// <param name="entries">The entries in file order, or null for none.</param>
    /// <returns>The mapped, dropped and manual entries, each in file order.</returns>
    public static PackageMapping Map(IReadOnlyList<PackageEntry>? entries)
    {
        var mapped = new List<PackageEntry>();
        var dropped = new List<UnmappedPackage>();
        var manual = new List<UnmappedPackage>();
        foreach (var entry in entries ?? [])
        {
            if (entry.Id.StartsWith("MonoGame.", StringComparison.OrdinalIgnoreCase))
            {
                dropped.Add(new UnmappedPackage(entry.Id, entry.Version, "Replaced by the MonoGame packages of the migrated project."));
            }
            else if (entry.Id.StartsWith("SharpDX", StringComparison.OrdinalIgnoreCase))
            {
                dropped.Add(new UnmappedPackage(entry.Id, entry.Version, "The MonoGame WindowsDX package brings its own SharpDX; direct use in code needs manual work."));
            }
            else if (IsUnsupportedMoniker(entry.TargetFramework))
            {
                manual.Add(new UnmappedPackage(entry.Id, entry.Version, $"Target framework '{entry.TargetFramework}' has no .NET 8 version; find a replacement by hand."));
            }
            else
            {
                mapped.Add(entry);
            }
        }

        return new PackageMapping(mapped, dropped, manual);
    }

    private static bool IsUnsupportedMoniker(string? moniker)
    {
        if (string.IsNullOrWhiteSpace(moniker))
        {
            return false;
        }

        var text = moniker.Trim();
        if (text.StartsWith("portable", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var digit = text.IndexOfAny("0123456789".ToCharArray());
        var name = digit < 0 ? text : text[..digit];
        name = name.Trim('-', '.');
        return Array.Exists(_unsupportedMonikers, m => string.Equals(m, name, StringComparison.OrdinalIgnoreCase));
    }
}
