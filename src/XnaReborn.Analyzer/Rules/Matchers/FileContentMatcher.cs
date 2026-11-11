using System.Text.RegularExpressions;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="FileContentMatch"/>: reads text files that match the glob and applies the regex. Read-only.</summary>
internal static class FileContentMatcher
{
    private const int _maxLinesPerFile = 3;

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Returns <c>relative/path:line</c> for the first matching lines of each matching file, in path order.
    /// Binary files (with a NUL byte), files over <see cref="FileContentMatch.MaxBytes"/> and unreadable files are skipped.
    /// </summary>
    /// <param name="match">The rule match.</param>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="projectDirectory">Absolute project directory.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public static IReadOnlyList<string> Match(FileContentMatch match, IReadOnlyFileSystem fileSystem, string projectDirectory, CancellationToken cancellationToken)
    {
        var regex = new Regex(match.Regex, RegexOptions.CultureInvariant, _regexTimeout);
        var results = new List<string>();
        foreach (var relative in FilePatternMatcher.Match(new FilePatternMatch(match.Glob), fileSystem, projectDirectory, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = ReadText(fileSystem, Path.Combine(projectDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), match.MaxBytes);
            if (text is null)
            {
                continue;
            }

            if (IsEffectFile(relative))
            {
                text = BlankComments(text);
            }

            foreach (var line in MatchingLines(regex, text).Take(_maxLinesPerFile))
            {
                results.Add($"{relative}:{line}");
            }
        }

        return results;
    }

    /// <summary>Replaces the characters of <c>//</c> and <c>/* */</c> comments with spaces. Line breaks and string literals stay, so positions and line numbers do not move.</summary>
    /// <param name="text">HLSL text.</param>
    internal static string BlankComments(string text)
    {
        var chars = text.ToCharArray();
        var i = 0;
        while (i < chars.Length)
        {
            if (chars[i] == '"')
            {
                i++;
                while (i < chars.Length && chars[i] != '"' && chars[i] != '\n')
                {
                    i++;
                }

                i++;
            }
            else if (chars[i] == '/' && i + 1 < chars.Length && chars[i + 1] == '/')
            {
                while (i < chars.Length && chars[i] != '\n')
                {
                    chars[i++] = ' ';
                }
            }
            else if (chars[i] == '/' && i + 1 < chars.Length && chars[i + 1] == '*')
            {
                chars[i++] = ' ';
                chars[i++] = ' ';
                while (i < chars.Length && !(chars[i] == '*' && i + 1 < chars.Length && chars[i + 1] == '/'))
                {
                    if (chars[i] != '\n' && chars[i] != '\r')
                    {
                        chars[i] = ' ';
                    }

                    i++;
                }

                for (var end = 0; end < 2 && i < chars.Length; end++)
                {
                    chars[i++] = ' ';
                }
            }
            else
            {
                i++;
            }
        }

        return new string(chars);
    }

    private static bool IsEffectFile(string relative) =>
        relative.EndsWith(".fx", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(".fxh", StringComparison.OrdinalIgnoreCase);

    private static string? ReadText(IReadOnlyFileSystem fileSystem, string path, int maxBytes)
    {
        try
        {
            using var stream = fileSystem.OpenRead(path);
            if (stream.Length > maxBytes)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            return Array.IndexOf(bytes, (byte)0) >= 0 ? null : fileSystem.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<int> MatchingLines(Regex regex, string text)
    {
        var last = 0;
        foreach (Match found in regex.Matches(text))
        {
            var line = LineOf(text, found.Index);
            if (line != last)
            {
                last = line;
                yield return line;
            }
        }
    }

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
