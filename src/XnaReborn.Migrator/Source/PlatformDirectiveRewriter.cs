using System.Text;
using System.Text.RegularExpressions;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>
/// Folds <c>#if</c> groups that depend on the platform symbols (WINDOWS, XBOX, XBOX360, ZUNE, WINDOWS_PHONE): the migrated
/// project defines none of them, so the branch for the migrated platform is kept and the other branches are removed.
/// It works on lines of text, so inactive branches stay visible and every untouched byte stays as it was.
/// </summary>
public sealed partial class PlatformDirectiveRewriter : ISourceRewriter
{
    /// <summary>The rule ID of the device conditional compilation rule.</summary>
    public const string Id = "XNA053";

    private const int _maxNesting = 64;

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var symbols = PlatformSymbolSet.For(context.Platform);
        if (symbols is null || !context.Text.Contains('#', StringComparison.Ordinal))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var lines = Scan(context.Text);
        if (!lines.Any(l => l.Keyword.Length > 0))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var run = new Run(context.Text, lines, symbols);
        try
        {
            run.Emit(0, lines.Count, 0);
        }
        catch (FormatException)
        {
            return Failed(context);
        }

        if (run.Folded == 0 && run.Marked == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var notes = new List<RewriteNote>();
        if (run.Folded > 0)
        {
            notes.Add(new RewriteNote(Id, $"Folded {run.Folded} platform conditional(s) for {context.Platform}.", NeedsManualWork: false));
        }

        notes.AddRange(run.Notes.Select(n => new RewriteNote(Id, n, NeedsManualWork: true)));
        return new SourceRewriteResult(run.Output.ToString(), notes);
    }

    [GeneratedRegex(@"^[ \t]*#[ \t]*(?<word>if|elif|else|endif)\b(?<rest>.*)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DirectivePattern();

    [GeneratedRegex(@"\b(WINDOWS|XBOX|XBOX360|ZUNE|WINDOWS_PHONE)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlatformWord();

    [GeneratedRegex(@"\bstatic\s+(?:void|int)\s+Main\s*\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EntryPoint();

    private static SourceRewriteResult Failed(SourceRewriteContext context)
    {
        var lines = Scan(context.Text);
        var first = lines.FirstOrDefault(l => l.Keyword == "if" && PlatformWord().IsMatch(l.Condition));
        if (first is null)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        const string message = "The #if directives of this file could not be read as a balanced set, so none were folded. Check them by hand.";
        var marker = MarkerText(context.Text, first, message);
        return marker is null
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(context.Text.Insert(first.Start, marker), [new RewriteNote(Id, message, NeedsManualWork: true)]);
    }

    // Splits the text into lines (terminators included) and marks the preprocessor directives outside comments and strings.
    private static List<Line> Scan(string text)
    {
        var lines = new List<Line>();
        var mode = Mode.Normal;
        var rawQuotes = 0;
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline + 1;
            var body = text[start..end].TrimEnd('\r', '\n');
            var keyword = string.Empty;
            var condition = string.Empty;
            if (mode == Mode.Normal && DirectivePattern().Match(body) is { Success: true } match)
            {
                keyword = match.Groups["word"].Value;
                var rest = match.Groups["rest"].Value;
                var comment = rest.IndexOf("//", StringComparison.Ordinal);
                condition = (comment < 0 ? rest : rest[..comment]).Trim();
            }
            else
            {
                mode = Advance(body, mode, ref rawQuotes);
            }

            lines.Add(new Line(start, end, keyword, condition));
            start = end;
        }

        return lines;
    }

    // Tracks block comments, verbatim strings and raw strings across lines. Regular strings and chars end on their line.
    private static Mode Advance(string line, Mode mode, ref int rawQuotes)
    {
        var i = 0;
        while (i < line.Length)
        {
            switch (mode)
            {
                case Mode.Block:
                    var close = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        return mode;
                    }

                    i = close + 2;
                    mode = Mode.Normal;
                    break;
                case Mode.Verbatim:
                    var quote = line.IndexOf('"', i);
                    if (quote < 0)
                    {
                        return mode;
                    }

                    if (quote + 1 < line.Length && line[quote + 1] == '"')
                    {
                        i = quote + 2;
                    }
                    else
                    {
                        i = quote + 1;
                        mode = Mode.Normal;
                    }

                    break;
                case Mode.Raw:
                    var closing = line.IndexOf(new string('"', rawQuotes), i, StringComparison.Ordinal);
                    if (closing < 0)
                    {
                        return mode;
                    }

                    i = closing + rawQuotes;
                    mode = Mode.Normal;
                    break;
                default:
                    var c = line[i];
                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        return mode;
                    }

                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                    {
                        mode = Mode.Block;
                        i += 2;
                    }
                    else if (IsVerbatimStart(line, i, out var length))
                    {
                        mode = Mode.Verbatim;
                        i += length;
                    }
                    else if (c == '"' && RunOf(line, i) >= 3)
                    {
                        rawQuotes = RunOf(line, i);
                        mode = Mode.Raw;
                        i += rawQuotes;
                    }
                    else if (c is '"' or '\'')
                    {
                        i = SkipQuoted(line, i, c);
                    }
                    else
                    {
                        i++;
                    }

                    break;
            }
        }

        return mode;
    }

    private static bool IsVerbatimStart(string line, int i, out int length)
    {
        length = 0;
        if (line[i] == '@' && i + 1 < line.Length && line[i + 1] == '"')
        {
            length = 2;
        }
        else if (i + 2 < line.Length && line[i + 2] == '"' && ((line[i] == '$' && line[i + 1] == '@') || (line[i] == '@' && line[i + 1] == '$')))
        {
            length = 3;
        }

        return length > 0;
    }

    private static int RunOf(string line, int i)
    {
        var n = 0;
        while (i + n < line.Length && line[i + n] == '"')
        {
            n++;
        }

        return n;
    }

    private static int SkipQuoted(string line, int i, char quote)
    {
        for (var j = i + 1; j < line.Length; j++)
        {
            if (line[j] == '\\')
            {
                j++;
            }
            else if (line[j] == quote)
            {
                return j + 1;
            }
        }

        return line.Length;
    }

    // The marker line to put above a directive, or null when the same marker is already there.
    private static string? MarkerText(string text, Line line, string message)
    {
        var directive = text[line.Start..line.End];
        var indent = directive[..(directive.Length - directive.TrimStart(' ', '\t').Length)];
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var marker = indent + MarkerInserter.Prefix + Id + ": " + message + newLine;
        return text.AsSpan(0, line.Start).EndsWith(marker, StringComparison.Ordinal) ? null : marker;
    }

    private enum Mode
    {
        Normal,
        Block,
        Verbatim,
        Raw,
    }

    private sealed record Line(int Start, int End, string Keyword, string Condition);

    private sealed record Branch(Line Directive, int Index, int BodyStart, int BodyEnd);

    private sealed class Run(string text, List<Line> lines, PlatformSymbolSet symbols)
    {
        public StringBuilder Output { get; } = new();

        public int Folded { get; private set; }

        public int Marked { get; private set; }

        public List<string> Notes { get; } = [];

        // Writes lines [from, to) to the output, folding the groups it finds. Throws FormatException on unbalanced directives.
        public void Emit(int from, int to, int depth)
        {
            if (depth > _maxNesting)
            {
                throw new FormatException("Too many nested #if groups.");
            }

            var i = from;
            while (i < to)
            {
                var line = lines[i];
                switch (line.Keyword)
                {
                    case "":
                        Output.Append(text, line.Start, line.End - line.Start);
                        i++;
                        break;
                    case "if":
                        i = Group(i, to, depth);
                        break;
                    default:
                        throw new FormatException($"Unexpected #{line.Keyword}.");
                }
            }
        }

        private int Group(int ifIndex, int limit, int depth)
        {
            var branches = new List<Branch>();
            var level = 0;
            var branchStart = ifIndex;
            var endIndex = -1;
            for (var i = ifIndex; i < limit; i++)
            {
                var keyword = lines[i].Keyword;
                if (keyword == "if")
                {
                    level++;
                    continue;
                }

                if (level == 1 && keyword is "elif" or "else")
                {
                    branches.Add(new Branch(lines[branchStart], branchStart, branchStart + 1, i));
                    branchStart = i;
                }
                else if (keyword == "endif")
                {
                    level--;
                    if (level == 0)
                    {
                        branches.Add(new Branch(lines[branchStart], branchStart, branchStart + 1, i));
                        endIndex = i;
                        break;
                    }
                }
            }

            if (endIndex < 0)
            {
                throw new FormatException("#if without #endif.");
            }

            Fold(branches, lines[endIndex], depth);
            return endIndex + 1;
        }

        private void Fold(List<Branch> branches, Line endif, int depth)
        {
            var results = branches
                .Select(b => b.Directive.Keyword == "else" ? new PlatformConditionResult(true, false, true) : PlatformCondition.Evaluate(b.Directive.Condition, symbols))
                .ToList();
            var mentions = branches.Zip(results).Any(p => p.Second?.MentionsPlatform ?? PlatformWord().IsMatch(p.First.Directive.Condition));

            if (!mentions || results.Any(r => r is null))
            {
                Keep(branches, endif, depth, mentions ? "This #if could not be read, so it was left as it is. Check it by hand." : null);
                return;
            }

            Branch? chosen = null;
            var undecided = false;
            for (var i = 0; i < branches.Count; i++)
            {
                var value = results[i]!.Value;
                if (value == true)
                {
                    chosen = branches[i];
                    break;
                }

                if (value is null)
                {
                    undecided = true;
                    break;
                }
            }

            if (undecided)
            {
                Keep(branches, endif, depth, $"This #if depends on a platform symbol and on another symbol, so it was left as it is. Migrated platform symbols: {string.Join(", ", symbols.TrueSymbols)}. Check it by hand.");
                return;
            }

            // The entry point must survive: an '#if WINDOWS || XBOX' around Main with no other branch is unwrapped, as ENTRYPOINT does.
            if (chosen is null && branches.Count == 1 && results[0]!.PlatformOnly && EntryPoint().IsMatch(text.AsSpan(lines[branches[0].BodyStart].Start, BodyLength(branches[0]))))
            {
                chosen = branches[0];
            }

            Folded++;
            if (chosen is not null)
            {
                Emit(chosen.BodyStart, chosen.BodyEnd, depth + 1);
            }
        }

        private int BodyLength(Branch branch) =>
            branch.BodyEnd <= branch.BodyStart ? 0 : lines[branch.BodyEnd].Start - lines[branch.BodyStart].Start;

        // Writes the group unchanged (but folds groups nested inside it), with a marker above it when a reason is given.
        private void Keep(List<Branch> branches, Line endif, int depth, string? markerMessage)
        {
            var ifLine = branches[0].Directive;
            if (markerMessage is not null && MarkerText(text, ifLine, markerMessage) is { } marker)
            {
                Output.Append(marker);
                Marked++;
                Notes.Add(markerMessage + $" (line {LineNumber(ifLine)})");
            }

            foreach (var branch in branches)
            {
                Output.Append(text, branch.Directive.Start, branch.Directive.End - branch.Directive.Start);
                Emit(branch.BodyStart, branch.BodyEnd, depth + 1);
            }

            Output.Append(text, endif.Start, endif.End - endif.Start);
        }

        private int LineNumber(Line line) => lines.IndexOf(line) + 1;
    }
}
