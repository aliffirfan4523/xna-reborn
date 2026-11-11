namespace XnaReborn.Migrator.Effects;

/// <summary>What kind of token the scanner found.</summary>
internal enum FxTokenKind
{
    /// <summary>A name or keyword.</summary>
    Identifier,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A string literal.</summary>
    String,

    /// <summary>One punctuation character.</summary>
    Punctuation,

    /// <summary>A whole preprocessor line.</summary>
    Preprocessor,
}

/// <summary>One token with its place in the text.</summary>
/// <param name="Kind">The token kind.</param>
/// <param name="Start">Offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
/// <param name="Line">1-based line of the first character.</param>
/// <param name="Text">The token text.</param>
internal sealed record FxToken(FxTokenKind Kind, int Start, int Length, int Line, string Text)
{
    /// <summary>Offset just after the last character.</summary>
    public int End => Start + Length;
}

/// <summary>A <c>compile</c> statement in a pass.</summary>
/// <param name="Model">The model token text: a profile such as <c>vs_2_0</c> or a macro name.</param>
/// <param name="ModelStart">Offset of the model token.</param>
/// <param name="ModelLength">Length of the model token.</param>
/// <param name="Function">The function name.</param>
/// <param name="HasArguments">True when the call has arguments.</param>
/// <param name="Line">1-based line of the statement.</param>
/// <param name="LineStart">Offset of the first character of that line.</param>
internal sealed record FxCompile(string Model, int ModelStart, int ModelLength, string Function, bool HasArguments, int Line, int LineStart);

/// <summary>A <c>#define</c> line.</summary>
/// <param name="Name">The macro name.</param>
/// <param name="Value">The replacement text without a trailing comment.</param>
/// <param name="Start">Offset of the <c>#</c>.</param>
/// <param name="Length">Length of the line without its line break.</param>
/// <param name="ConditionalDepth">How many <c>#if</c> blocks are open at the line.</param>
/// <param name="Line">1-based line.</param>
internal sealed record FxDefine(string Name, string Value, int Start, int Length, int ConditionalDepth, int Line);

/// <summary>An <c>#include</c> line.</summary>
/// <param name="Path">The path between the quotes or angle brackets.</param>
/// <param name="Start">Offset of the <c>#</c>.</param>
/// <param name="Length">Length of the line without its line break.</param>
/// <param name="Line">1-based line.</param>
internal sealed record FxInclude(string Path, int Start, int Length, int Line);

/// <summary>A <c>Key = Value;</c> statement inside a pass or a sampler state.</summary>
/// <param name="Key">The key text.</param>
/// <param name="KeyStart">Offset of the key.</param>
/// <param name="Value">The value text.</param>
/// <param name="ValueStart">Offset of the value.</param>
/// <param name="ValueLength">Length of the value.</param>
/// <param name="Line">1-based line of the key.</param>
/// <param name="LineStart">Offset of the first character of that line.</param>
internal sealed record FxStatement(string Key, int KeyStart, string Value, int ValueStart, int ValueLength, int Line, int LineStart);

/// <summary>A place that gets a marker: an annotation, a Direct3D 10 shader call, or a <c>VPOS</c> input.</summary>
/// <param name="Text">The identifier or a short description.</param>
/// <param name="Line">1-based line.</param>
/// <param name="LineStart">Offset of the first character of that line.</param>
internal sealed record FxSpot(string Text, int Line, int LineStart);

/// <summary>A top-level variable.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="HasDefault">True when the declaration assigns a value.</param>
/// <param name="IsUsed">True when the name appears anywhere outside its declaration.</param>
/// <param name="Line">1-based line.</param>
internal sealed record FxGlobal(string Name, bool HasDefault, bool IsUsed, int Line);

/// <summary>What <see cref="FxScanner"/> found in one effect file.</summary>
internal sealed record FxDocument
{
    /// <summary>The scanned text.</summary>
    public required string Text { get; init; }

    /// <summary>The <c>compile</c> statements in file order.</summary>
    public IReadOnlyList<FxCompile> Compiles { get; init; } = [];

    /// <summary>The <c>#define</c> lines in file order.</summary>
    public IReadOnlyList<FxDefine> Defines { get; init; } = [];

    /// <summary>The <c>#include</c> lines in file order.</summary>
    public IReadOnlyList<FxInclude> Includes { get; init; } = [];

    /// <summary>The statements inside <c>sampler_state</c> blocks in file order.</summary>
    public IReadOnlyList<FxStatement> SamplerStates { get; init; } = [];

    /// <summary>The statements inside passes that are not shader assignments, in file order.</summary>
    public IReadOnlyList<FxStatement> PassStates { get; init; } = [];

    /// <summary>Annotation blocks (<c>&lt; ... &gt;</c> after a declaration, technique or pass).</summary>
    public IReadOnlyList<FxSpot> Annotations { get; init; } = [];

    /// <summary>Uses of <c>SetVertexShader</c>, <c>SetPixelShader</c> or <c>SetGeometryShader</c>.</summary>
    public IReadOnlyList<FxSpot> SetShaderCalls { get; init; } = [];

    /// <summary>Uses of <c>VPOS</c>.</summary>
    public IReadOnlyList<FxSpot> Vpos { get; init; } = [];

    /// <summary>Top-level variables that are not static or constant, and not samplers.</summary>
    public IReadOnlyList<FxGlobal> Globals { get; init; } = [];

    /// <summary>Number of techniques.</summary>
    public int TechniqueCount { get; init; }

    /// <summary>Number of passes.</summary>
    public int PassCount { get; init; }

    /// <summary>Offset of the first line that holds code: not blank, not a comment and not an <c>#include</c>.</summary>
    public int FirstCodeOffset { get; init; }

    /// <summary>The line break the file uses.</summary>
    public string NewLine { get; init; } = "\n";
}
