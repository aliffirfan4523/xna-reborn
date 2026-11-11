using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>The input of one <see cref="ISourceRewriter"/> call.</summary>
/// <param name="FilePath">Path of the file relative to the project root, with '/' separators.</param>
/// <param name="Text">Current text of the file.</param>
/// <param name="OriginalSymbols">The original project's DefineConstants, such as WINDOWS, DEBUG and TRACE.</param>
/// <param name="Platform">The platform of the project that is migrated. Unknown when it is not stated.</param>
/// <param name="Language">The language of the file. C# unless stated.</param>
/// <param name="ImplicitImports">Namespaces the project imports for every file (Visual Basic project-level imports), or null.</param>
public sealed record SourceRewriteContext(
    string FilePath,
    string Text,
    IReadOnlyCollection<string> OriginalSymbols,
    XnaPlatform Platform = XnaPlatform.Unknown,
    SourceLanguage Language = SourceLanguage.CSharp,
    IReadOnlyList<string>? ImplicitImports = null);
