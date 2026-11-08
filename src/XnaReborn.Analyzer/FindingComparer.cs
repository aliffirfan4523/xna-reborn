using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer;

/// <summary>Orders findings by severity (Blocker first), rule id, file, line and column.</summary>
internal sealed class FindingComparer : IComparer<Finding>
{
    /// <summary>Shared instance.</summary>
    public static readonly FindingComparer Instance = new();

    /// <inheritdoc />
    public int Compare(Finding? x, Finding? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var c = y.Severity.CompareTo(x.Severity);
        if (c != 0)
        {
            return c;
        }

        c = string.CompareOrdinal(x.RuleId, y.RuleId);
        if (c != 0)
        {
            return c;
        }

        c = (x.Location is null ? 0 : 1).CompareTo(y.Location is null ? 0 : 1);
        if (c != 0)
        {
            return c;
        }

        c = string.CompareOrdinal(x.Project, y.Project);
        if (c != 0)
        {
            return c;
        }

        c = string.CompareOrdinal(x.Location?.File, y.Location?.File);
        if (c != 0)
        {
            return c;
        }

        c = (x.Location?.Line ?? 0).CompareTo(y.Location?.Line ?? 0);
        if (c != 0)
        {
            return c;
        }

        c = (x.Location?.Column ?? 0).CompareTo(y.Location?.Column ?? 0);
        return c != 0 ? c : string.CompareOrdinal(x.Snippet, y.Snippet);
    }
}

/// <summary>Orders diagnostics by code, file (null first) and message, all ordinal.</summary>
internal sealed class DiagnosticComparer : IComparer<Diagnostic>
{
    /// <summary>Shared instance.</summary>
    public static readonly DiagnosticComparer Instance = new();

    /// <inheritdoc />
    public int Compare(Diagnostic? x, Diagnostic? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var c = string.CompareOrdinal(x.Code, y.Code);
        if (c != 0)
        {
            return c;
        }

        c = string.CompareOrdinal(x.File, y.File);
        return c != 0 ? c : string.CompareOrdinal(x.Message, y.Message);
    }
}
