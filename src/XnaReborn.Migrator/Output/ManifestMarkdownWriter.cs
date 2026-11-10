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

        if (manifest.Content is not null)
        {
            blocks.Add(ContentBlock(manifest.Content));
        }

        if (manifest.Shims is not null)
        {
            blocks.Add(ShimsBlock(manifest.Shims));
        }

        if (manifest.Modern is not null)
        {
            blocks.Add(ModernBlock(manifest.Modern));
        }

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

    private static string ContentBlock(ManifestContent content)
    {
        var sb = new StringBuilder("## Content\n\n| Status | Assets |\n|---|---|");
        foreach (var (status, count) in content.CountsByStatus)
        {
            sb.Append("\n| ").Append(Cell(status)).Append(" | ").Append(count).Append(" |");
        }

        sb.Append("\n\n| Type | Assets |\n|---|---|");
        foreach (var (type, count) in content.CountsByType)
        {
            sb.Append("\n| ").Append(Cell(type)).Append(" | ").Append(count).Append(" |");
        }

        return sb.ToString();
    }

    private static string ShimsBlock(ManifestShims shims)
    {
        var sb = new StringBuilder("## Shims\n\n");
        sb.Append("SHIM: these XNA APIs are dead. The library `").Append(shims.Project)
            .Append("` gives them offline stand-ins (local files, one offline player). They are not the real XNA services.");
        sb.Append("\n\nShim files:");
        foreach (var file in shims.Files)
        {
            sb.Append("\n- `").Append(file).Append('`');
        }

        sb.Append("\n\nReferenced by: ").Append(shims.ReferencedBy.Count == 0 ? "no project." : string.Join(", ", shims.ReferencedBy.Select(p => $"`{p}`")));
        sb.Append("\n\nShim use in game source:");
        if (shims.Uses.Count == 0)
        {
            return sb.Append(" none.").ToString();
        }

        sb.Append("\n\n| File | Shimmed namespace | Types |\n|---|---|---|");
        foreach (var use in shims.Uses)
        {
            sb.Append("\n| ").Append(Cell(use.File)).Append(" | ").Append(Cell(use.Namespace))
                .Append(" | ").Append(Cell(use.Types.Count == 0 ? null : string.Join(", ", use.Types))).Append(" |");
        }

        return sb.ToString();
    }

    private static string ModernBlock(ManifestModern modern)
    {
        var sb = new StringBuilder("## Modern extras\n\n");
        sb.Append("Opt-in window extras: ").Append(string.Join(", ", modern.Features)).Append(".\n\nHelper files:");
        foreach (var file in modern.Files)
        {
            sb.Append("\n- `").Append(file).Append('`');
        }

        sb.Append("\n\nHook added to:");
        if (modern.HookedFiles.Count == 0)
        {
            sb.Append(" no file. No entry point could take the one-line hook, so the extras do nothing.");
        }
        else
        {
            foreach (var file in modern.HookedFiles)
            {
                sb.Append("\n- `").Append(file).Append('`');
            }
        }

        sb.Append("\n\nLimits:");
        foreach (var limit in modern.Limits)
        {
            sb.Append("\n- ").Append(limit);
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
