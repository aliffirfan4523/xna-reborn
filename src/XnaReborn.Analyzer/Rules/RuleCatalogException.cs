namespace XnaReborn.Analyzer.Rules;

/// <summary>Thrown when a rule catalog cannot be read or fails validation. Nothing is loaded from an invalid catalog.</summary>
public sealed class RuleCatalogException : Exception
{
    /// <summary>Creates an exception that carries every problem found.</summary>
    /// <param name="message">Summary message.</param>
    /// <param name="errors">One line per problem.</param>
    public RuleCatalogException(string message, IReadOnlyList<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>One line per problem. Each line names the rule id (or rule number) and the JSON path.</summary>
    public IReadOnlyList<string> Errors { get; }
}
