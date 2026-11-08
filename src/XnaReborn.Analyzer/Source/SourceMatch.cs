using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Source;

/// <summary>One place in a syntax tree where a source rule matched.</summary>
/// <param name="Rule">The rule that matched.</param>
/// <param name="Line">1-based line number.</param>
/// <param name="Column">1-based column number.</param>
/// <param name="Snippet">The trimmed source line, at most 200 characters.</param>
internal sealed record SourceMatch(Rule Rule, int Line, int Column, string Snippet);
