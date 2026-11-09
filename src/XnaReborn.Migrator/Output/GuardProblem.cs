namespace XnaReborn.Migrator.Output;

/// <summary>Why <see cref="OutputGuard"/> refused an output location.</summary>
public enum GuardProblem
{
    /// <summary>No problem. The output location is allowed.</summary>
    None,

    /// <summary>A path is empty, malformed, or the output is a drive root.</summary>
    InvalidPath,

    /// <summary>The output folder is the source folder.</summary>
    OutputIsSource,

    /// <summary>The output folder is inside the source folder.</summary>
    OutputInsideSource,

    /// <summary>The source folder is inside the output folder.</summary>
    SourceInsideOutput,

    /// <summary>The output path is an existing file.</summary>
    OutputIsAFile,

    /// <summary>The output folder exists, has content, and the user has not confirmed replacing it.</summary>
    OutputNotEmpty,

    /// <summary>The source folder does not exist.</summary>
    SourceMissing,
}
