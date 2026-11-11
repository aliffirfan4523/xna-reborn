namespace XnaReborn.Migrator.Shims;

/// <summary>
/// The C# files of the XACT stand-in (<c>XnaReborn.Compat.Xact</c>). They live as real source files under <c>Shims/Xact</c> so that the
/// tests compile and run them against MonoGame; the build embeds them and this class reads them back.
/// </summary>
public static class XactShimSources
{
    private const string _prefix = "XnaReborn.Migrator.Shims.Xact.";

    private static readonly Lazy<IReadOnlyList<(string Name, string Text)>> _files = new(Load);

    /// <summary>Gets the shim files, sorted by file name. Each name ends in <c>.cs</c>.</summary>
    public static IReadOnlyList<(string Name, string Text)> Files => _files.Value;

    private static IReadOnlyList<(string Name, string Text)> Load()
    {
        var assembly = typeof(XactShimSources).Assembly;
        var files = new List<(string Name, string Text)>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(_prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("Embedded resource " + resource + " is missing.");
            using var reader = new StreamReader(stream);
            files.Add((resource[_prefix.Length..], reader.ReadToEnd().ReplaceLineEndings("\n")));
        }

        return files;
    }
}
