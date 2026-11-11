namespace XnaReborn.Analyzer.Source;

/// <summary>A member access or call as a language independent fact.</summary>
/// <param name="Name">The member name.</param>
/// <param name="LeftText">Dotted text of the expression left of the dot, or null when it is not a pure name chain (or empty, as in a Visual Basic <c>With</c> block).</param>
/// <param name="Receiver">The identifier directly left of the dot, or null.</param>
/// <param name="IsInvocation">True when the access is called.</param>
/// <param name="ArgumentCount">Number of arguments of the call, 0 when it is not a call.</param>
/// <param name="Position">Character offset of the start of the access.</param>
internal readonly record struct MemberUse(string Name, string? LeftText, string? Receiver, bool IsInvocation, int ArgumentCount, int Position);

/// <summary>
/// Receives the language independent facts a walker finds in one syntax tree. The rule logic (namespace gates, name and shape matching)
/// lives in the sink, so one set of rules serves C# and Visual Basic.
/// </summary>
internal interface ISourceFactSink
{
    /// <summary>Gets a value indicating whether the sink wants preprocessor symbol facts.</summary>
    bool WantsPreprocessorSymbols { get; }

    /// <summary>A namespace the file imports (<c>using</c> or <c>Imports</c>). Sent for all imports before any other fact.</summary>
    /// <param name="dottedNamespace">The namespace.</param>
    void DeclareImport(string dottedNamespace);

    /// <summary>An import alias (<c>using X = A.B;</c> or <c>Imports X = A.B</c>). Sent before any other fact.</summary>
    /// <param name="alias">The alias.</param>
    /// <param name="target">The dotted target.</param>
    void Alias(string alias, string target);

    /// <summary>An import statement as code, where a namespace rule can match.</summary>
    /// <param name="dottedNamespace">The imported name.</param>
    /// <param name="position">Character offset of the statement.</param>
    void UsingDirective(string dottedNamespace, int position);

    /// <summary>The longest pure name chain at a position.</summary>
    /// <param name="dottedText">The chain.</param>
    /// <param name="position">Character offset of the start.</param>
    void NamespaceChain(string dottedText, int position);

    /// <summary>A simple name that might be a type.</summary>
    /// <param name="name">The name.</param>
    /// <param name="leftText">Dotted text left of it, or null.</param>
    /// <param name="qualified">True when the name is the right part of a dotted name.</param>
    /// <param name="position">Character offset of the name.</param>
    void SimpleName(string name, string? leftText, bool qualified, int position);

    /// <summary>A member access or call.</summary>
    /// <param name="use">The access.</param>
    void MemberAccess(MemberUse use);

    /// <summary>A method declaration.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="isOverride">True when it carries the override modifier.</param>
    /// <param name="position">Character offset of the method name.</param>
    void MethodDeclaration(string name, bool isOverride, int position);

    /// <summary>A symbol named in an <c>#if</c> or <c>#elif</c> condition.</summary>
    /// <param name="name">The symbol.</param>
    /// <param name="position">Character offset of the symbol.</param>
    void PreprocessorSymbol(string name, int position);
}

/// <summary>Walks one syntax tree and reports facts to a sink.</summary>
internal interface ISourceWalker
{
    /// <summary>Walks a tree once.</summary>
    /// <param name="root">The root of the tree.</param>
    /// <param name="sink">Receives the facts.</param>
    void Walk(Microsoft.CodeAnalysis.SyntaxNode root, ISourceFactSink sink);
}
