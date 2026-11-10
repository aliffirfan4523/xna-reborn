using System.Text;

namespace XnaReborn.Migrator.Output;

/// <summary>Writes a <see cref="Manifest"/> as a Markdown document.</summary>
public static class ManifestMarkdownWriter
{
    /// <summary>Converts the manifest to Markdown with LF line endings and one trailing newline.</summary>
    /// <param name="manifest">The manifest to write.</param>
    /// <param name="manualNotes">Extra manual-work notes, listed after the manual files.</param>
    /// <returns>The Markdown text.</returns>
    public static string Write(Manifest manifest, IReadOnlyList<string> manualNotes)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manualNotes);

        var blocks = new List<string>
        {
            "# XNA Reborn migration manifest",
            manifest.Completed ? "Status: Completed" : "Status: Incomplete: the migration did not finish",
            $"Rule catalog: {manifest.CatalogName}. Migrator {manifest.Migrator}. Manifest format {manifest.Version}.",
            CountsBlock(manifest),
            ManualBlock(manifest, manualNotes),
            ChangedBlock(manifest),
            CopiedAndSkippedBlock(manifest),
        };

        if (manifest.Build is not null)
        {
            blocks.Add(BuildBlock(manifest.Build));
        }

        return string.Join("\n\n", blocks).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static string CountsBlock(Manifest manifest)
    {
        var sb = new StringBuilder("## Summary\n\n| Kind | Files |\n|---|---|");
        foreach (var kind in Enum.GetValues<ChangeKind>())
        {
            sb.Append("\n| ").Append(kind).Append(" | ")
                .Append(manifest.Changes.Count(c => c.Kind == kind)).Append(" |");
        }

        return sb.ToString();
    }

    private static string ManualBlock(Manifest manifest, IReadOnlyList<string> manualNotes)
    {
        var lines = manifest.Changes
            .Where(c => c.Kind == ChangeKind.ManualWork)
            .Select(c => $"- `{c.Path}`" + (string.IsNullOrEmpty(c.Reason) ? string.Empty : $": {c.Reason}"))
            .Concat(manualNotes.Select(n => $"- {n}"))
            .ToList();
        return "## Manual work\n\n" + (lines.Count == 0 ? "None." : string.Join('\n', lines));
    }

    private static string ChangedBlock(Manifest manifest)
    {
        var changed = manifest.Changes.Where(c => c.Kind is ChangeKind.Created or ChangeKind.Modified).ToList();
        if (changed.Count == 0)
        {
            return "## Changed files\n\nNone.";
        }

        var sb = new StringBuilder("## Changed files\n\n| File | Kind | Rules |\n|---|---|---|");
        foreach (var c in changed)
        {
            sb.Append("\n| ").Append(Cell(c.Path))
                .Append(" | ").Append(c.Kind)
                .Append(" | ").Append(Cell(c.Rules.Count == 0 ? null : string.Join(", ", c.Rules)))
                .Append(" |");
        }

        return sb.ToString();
    }

    private static string CopiedAndSkippedBlock(Manifest manifest)
    {
        var copied = manifest.Changes.Count(c => c.Kind == ChangeKind.Copied);
        var skipped = manifest.Changes.Where(c => c.Kind == ChangeKind.Skipped).ToList();
        var sb = new StringBuilder("## Copied and skipped\n\n");
        sb.Append($"Copied unchanged: {copied}\n\nSkipped: {skipped.Count}");
        var first = true;
        foreach (var s in skipped)
        {
            sb.Append(first ? "\n\n- `" : "\n- `").Append(s.Path).Append('`');
            first = false;
            if (!string.IsNullOrEmpty(s.Reason))
            {
                sb.Append(": ").Append(s.Reason);
            }
        }

        return sb.ToString();
    }

    private static string BuildBlock(ManifestBuild build)
    {
        var sb = new StringBuilder("## Build\n\n");
        sb.Append(!build.Attempted ? "Not attempted." : build.Succeeded ? "Succeeded." : "Failed.");
        if (build.Errors.Count > 0)
        {
            sb.Append('\n');
        }
        foreach (var e in build.Errors)
        {
            sb.Append("\n- ").Append(e);
        }

        return sb.ToString();
    }

    private static string Cell(string? text) =>
        string.IsNullOrEmpty(text) ? "-" : text.Replace("|", "\\|", StringComparison.Ordinal);
}
