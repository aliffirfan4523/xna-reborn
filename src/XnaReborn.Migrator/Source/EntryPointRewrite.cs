namespace XnaReborn.Migrator.Source;

/// <summary>Outcome of an entry point rewrite.</summary>
public enum EntryPointStatus
{
    /// <summary>The active code has no entry point.</summary>
    NoEntryPoint,

    /// <summary>The entry point is not inside a conditional directive, so nothing needed changing.</summary>
    Unchanged,

    /// <summary>Platform conditionals around the entry point were removed.</summary>
    Unwrapped,

    /// <summary>The entry point is inside a conditional that cannot be removed safely. The text is unchanged.</summary>
    Skipped,
}

/// <summary>Result of <see cref="EntryPointRewriter.Rewrite"/>.</summary>
/// <param name="Text">The source text after the rewrite. Equal to the input unless <paramref name="Status"/> is <see cref="EntryPointStatus.Unwrapped"/>.</param>
/// <param name="Status">What happened.</param>
/// <param name="Note">Explanation for a person, or null when there is nothing to say.</param>
public sealed record EntryPointRewrite(string Text, EntryPointStatus Status, string? Note);
