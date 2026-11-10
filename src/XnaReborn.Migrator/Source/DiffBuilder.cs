using System.Text;
using DiffPlex;
using DiffPlex.Chunkers;

namespace XnaReborn.Migrator.Source;

/// <summary>Builds unified and side-by-side line diffs with DiffPlex. Pure and deterministic.</summary>
/// <remarks>
/// Lines are compared with their CR kept, so a CRLF to LF change is reported as changed lines.
/// A missing final newline is not reported.
/// </remarks>
public static class DiffBuilder
{
    private enum Op
    {
        Keep,
        Delete,
        Insert,
    }

    /// <summary>Builds a unified diff with LF newlines, or an empty string when the texts are equal.</summary>
    /// <param name="path">Relative path shown in the headers.</param>
    /// <param name="original">Original text.</param>
    /// <param name="updated">Updated text.</param>
    /// <param name="contextLines">Unchanged lines shown around each change.</param>
    /// <returns>The diff text, ending with a newline, or an empty string.</returns>
    public static string BuildUnified(string path, string original, string updated, int contextLines = 3)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(updated);
        ArgumentOutOfRangeException.ThrowIfNegative(contextLines);

        var (oldLines, newLines, ops) = Compute(original, updated);
        if (ops.All(o => o == Op.Keep))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("--- a/").Append(path).Append('\n');
        sb.Append("+++ b/").Append(path).Append('\n');

        var i = 0;
        while (i < ops.Count)
        {
            if (ops[i] == Op.Keep)
            {
                i++;
                continue;
            }

            var start = Math.Max(0, i - contextLines);
            var end = i;
            var lastChange = i;
            while (end < ops.Count && end - lastChange <= contextLines * 2)
            {
                if (ops[end] != Op.Keep)
                {
                    lastChange = end;
                }

                end++;
            }

            end = Math.Min(ops.Count, lastChange + 1 + contextLines);
            AppendHunk(sb, ops, oldLines, newLines, start, end);
            i = end;
        }

        return sb.ToString();
    }

    /// <summary>Builds an aligned side-by-side diff.</summary>
    /// <param name="path">Relative path of the file.</param>
    /// <param name="original">Original text.</param>
    /// <param name="updated">Updated text.</param>
    /// <returns>Two equally long line lists with counts.</returns>
    public static FileDiff BuildSideBySide(string path, string original, string updated)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(updated);

        var (oldLines, newLines, ops) = Compute(original, updated);
        var left = new List<DiffLine>();
        var right = new List<DiffLine>();
        int added = 0, removed = 0, modified = 0;
        int o = 0, n = 0, i = 0;
        while (i < ops.Count)
        {
            if (ops[i] == Op.Keep)
            {
                left.Add(new DiffLine(o + 1, Display(oldLines[o]), DiffLineKind.Unchanged));
                right.Add(new DiffLine(n + 1, Display(newLines[n]), DiffLineKind.Unchanged));
                o++;
                n++;
                i++;
                continue;
            }

            var dels = 0;
            var ins = 0;
            while (i < ops.Count && ops[i] != Op.Keep)
            {
                if (ops[i] == Op.Delete)
                {
                    dels++;
                }
                else
                {
                    ins++;
                }

                i++;
            }

            var pairs = Math.Min(dels, ins);
            for (var k = 0; k < Math.Max(dels, ins); k++)
            {
                var hasOld = k < dels;
                var hasNew = k < ins;
                var kind = k < pairs ? DiffLineKind.Modified : (hasOld ? DiffLineKind.Removed : DiffLineKind.Added);
                left.Add(hasOld
                    ? new DiffLine(o + k + 1, Display(oldLines[o + k]), kind)
                    : new DiffLine(null, string.Empty, DiffLineKind.Imaginary));
                right.Add(hasNew
                    ? new DiffLine(n + k + 1, Display(newLines[n + k]), kind)
                    : new DiffLine(null, string.Empty, DiffLineKind.Imaginary));
            }

            modified += pairs;
            removed += dels - pairs;
            added += ins - pairs;
            o += dels;
            n += ins;
        }

        return new FileDiff(path, left, right, added, removed, modified);
    }

    private static string Display(string line) => line.TrimEnd('\r');

    private static void AppendHunk(
        StringBuilder sb, List<Op> ops, List<string> oldLines, List<string> newLines, int start, int end)
    {
        int o = 0, n = 0;
        for (var k = 0; k < start; k++)
        {
            if (ops[k] != Op.Insert)
            {
                o++;
            }

            if (ops[k] != Op.Delete)
            {
                n++;
            }
        }

        int oldStart = o, newStart = n, oldCount = 0, newCount = 0;
        var body = new StringBuilder();
        for (var k = start; k < end; k++)
        {
            switch (ops[k])
            {
                case Op.Keep:
                    body.Append(' ').Append(Display(oldLines[o])).Append('\n');
                    o++;
                    n++;
                    oldCount++;
                    newCount++;
                    break;
                case Op.Delete:
                    body.Append('-').Append(Display(oldLines[o])).Append('\n');
                    o++;
                    oldCount++;
                    break;
                default:
                    body.Append('+').Append(Display(newLines[n])).Append('\n');
                    n++;
                    newCount++;
                    break;
            }
        }

        sb.Append("@@ -").Append(Range(oldStart, oldCount)).Append(" +").Append(Range(newStart, newCount)).Append(" @@\n");
        sb.Append(body);
    }

    // Unified format: an empty range is written as "<line before>,0".
    private static string Range(int zeroBasedStart, int count) =>
        count == 0 ? $"{zeroBasedStart},0" : count == 1 ? $"{zeroBasedStart + 1}" : $"{zeroBasedStart + 1},{count}";

    private static (List<string> Old, List<string> New, List<Op> Ops) Compute(string original, string updated)
    {
        var oldLines = SplitLines(original);
        var newLines = SplitLines(updated);
        var result = Differ.Instance.CreateDiffs(
            string.Join('\n', oldLines.Select(Key)),
            string.Join('\n', newLines.Select(Key)),
            ignoreWhiteSpace: false,
            ignoreCase: false,
            new NewlineChunker());

        var ops = new List<Op>();
        int a = 0, b = 0;
        foreach (var block in result.DiffBlocks)
        {
            while (a < block.DeleteStartA)
            {
                ops.Add(Op.Keep);
                a++;
                b++;
            }

            for (var k = 0; k < block.DeleteCountA; k++)
            {
                ops.Add(Op.Delete);
            }

            for (var k = 0; k < block.InsertCountB; k++)
            {
                ops.Add(Op.Insert);
            }

            a += block.DeleteCountA;
            b += block.InsertCountB;
        }

        while (a < oldLines.Count)
        {
            ops.Add(Op.Keep);
            a++;
            b++;
        }

        return (oldLines, newLines, ops);
    }

    // Prefix keeps empty lines distinct from an empty text.
    private static string Key(string line) => "|" + line;

    private static List<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n').ToList();
        if (lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private sealed class NewlineChunker : IChunker
    {
        public IReadOnlyList<string> Chunk(string str) => str.Length == 0 ? [] : str.Split('\n');
    }
}
