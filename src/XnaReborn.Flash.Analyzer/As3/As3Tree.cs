namespace XnaReborn.Flash.Analyzer.As3;

/// <summary>Base of every syntax tree node. Every node knows where it came from.</summary>
/// <param name="Span">The source range the node covers.</param>
public abstract record As3Node(SourceSpan Span);

/// <summary>Modifiers of a declaration.</summary>
[Flags]
public enum As3Modifier
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>The <c>public</c> keyword.</summary>
    Public = 1,

    /// <summary>The <c>private</c> keyword.</summary>
    Private = 2,

    /// <summary>The <c>protected</c> keyword.</summary>
    Protected = 4,

    /// <summary>The <c>internal</c> keyword.</summary>
    Internal = 8,

    /// <summary>The <c>static</c> modifier.</summary>
    Static = 16,

    /// <summary>The <c>override</c> modifier.</summary>
    Override = 32,

    /// <summary>The <c>final</c> modifier.</summary>
    Final = 64,

    /// <summary>The <c>dynamic</c> modifier.</summary>
    Dynamic = 128,

    /// <summary>The <c>native</c> modifier.</summary>
    Native = 256,
}

/// <summary>A type annotation such as <c>int</c>, <c>org.flixel.FlxSprite</c>, <c>*</c> or <c>Vector.&lt;int&gt;</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">The dotted name, or "*".</param>
/// <param name="TypeArguments">Type arguments of a <c>Vector.&lt;T&gt;</c>, empty otherwise.</param>
public sealed record As3TypeRef(SourceSpan Span, string Name, IReadOnlyList<As3TypeRef> TypeArguments) : As3Node(Span);

/// <summary>One argument of a metadata tag, such as <c>source="a.png"</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Key">The key, or null for a bare value.</param>
/// <param name="Value">The value without quotes.</param>
public sealed record As3MetadataArgument(SourceSpan Span, string? Key, string Value) : As3Node(Span);

/// <summary>A metadata tag such as <c>[Embed(source="a.png")]</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">The tag name.</param>
/// <param name="Arguments">The arguments.</param>
public sealed record As3Metadata(SourceSpan Span, string Name, IReadOnlyList<As3MetadataArgument> Arguments) : As3Node(Span)
{
    /// <summary>Returns the value of the argument called <paramref name="key"/>, or null.</summary>
    /// <param name="key">Argument key, compared ignoring case.</param>
    public string? Get(string key) =>
        Arguments.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
}

/// <summary>An <c>import</c> directive.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">The dotted name, without the trailing ".*".</param>
/// <param name="IsWildcard">True for <c>import a.b.*</c>.</param>
public sealed record As3Import(SourceSpan Span, string Name, bool IsWildcard) : As3Node(Span);

/// <summary>A parse problem.</summary>
/// <param name="Message">What was expected.</param>
/// <param name="Span">Where it happened.</param>
public sealed record As3Diagnostic(string Message, SourceSpan Span);

/// <summary>One parsed <c>.as</c> file.</summary>
/// <param name="Span">Source range.</param>
/// <param name="PackageName">The package name, empty for the top-level package or when there is no package block.</param>
/// <param name="Imports">Every import in the file.</param>
/// <param name="Members">Types, package-level functions and variables, in source order.</param>
/// <param name="Diagnostics">Syntax problems found.</param>
public sealed record As3CompilationUnit(
    SourceSpan Span,
    string PackageName,
    IReadOnlyList<As3Import> Imports,
    IReadOnlyList<As3Member> Members,
    IReadOnlyList<As3Diagnostic> Diagnostics) : As3Node(Span);

/// <summary>Base of declarations that live in a package or class body.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Metadata">Metadata tags in front of the declaration.</param>
/// <param name="Modifiers">Declaration modifiers.</param>
public abstract record As3Member(SourceSpan Span, IReadOnlyList<As3Metadata> Metadata, As3Modifier Modifiers) : As3Node(Span);

/// <summary>Class or interface.</summary>
public enum As3TypeKind
{
    /// <summary>A class.</summary>
    Class,

    /// <summary>An interface.</summary>
    Interface,
}

/// <summary>A class or interface declaration.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Metadata">Metadata tags.</param>
/// <param name="Modifiers">Modifiers.</param>
/// <param name="Kind">Class or interface.</param>
/// <param name="Name">Type name.</param>
/// <param name="BaseType">The <c>extends</c> type of a class, or null.</param>
/// <param name="Interfaces">Implemented interfaces, or extended interfaces for an interface.</param>
/// <param name="Members">The body members.</param>
public sealed record As3TypeDeclaration(
    SourceSpan Span,
    IReadOnlyList<As3Metadata> Metadata,
    As3Modifier Modifiers,
    As3TypeKind Kind,
    string Name,
    As3TypeRef? BaseType,
    IReadOnlyList<As3TypeRef> Interfaces,
    IReadOnlyList<As3Member> Members) : As3Member(Span, Metadata, Modifiers);

/// <summary>A <c>var</c> or <c>const</c> declared as a member.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Metadata">Metadata tags such as Embed.</param>
/// <param name="Modifiers">Modifiers.</param>
/// <param name="IsConst">True for <c>const</c>.</param>
/// <param name="Name">Field name.</param>
/// <param name="Type">Declared type, or null when untyped.</param>
/// <param name="Initializer">Initial value, or null.</param>
public sealed record As3Field(
    SourceSpan Span,
    IReadOnlyList<As3Metadata> Metadata,
    As3Modifier Modifiers,
    bool IsConst,
    string Name,
    As3TypeRef? Type,
    As3Expression? Initializer) : As3Member(Span, Metadata, Modifiers);

/// <summary>What a <see cref="As3Method"/> is.</summary>
public enum As3MethodKind
{
    /// <summary>An ordinary function.</summary>
    Normal,

    /// <summary>A <c>get</c> accessor.</summary>
    Getter,

    /// <summary>A <c>set</c> accessor.</summary>
    Setter,

    /// <summary>The constructor (a function named like its class).</summary>
    Constructor,
}

/// <summary>A function parameter.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">Parameter name.</param>
/// <param name="Type">Declared type, or null.</param>
/// <param name="Default">Default value, or null.</param>
/// <param name="IsRest">True for <c>...args</c>.</param>
public sealed record As3Parameter(SourceSpan Span, string Name, As3TypeRef? Type, As3Expression? Default, bool IsRest) : As3Node(Span);

/// <summary>A function or method declaration.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Metadata">Metadata tags.</param>
/// <param name="Modifiers">Modifiers.</param>
/// <param name="Kind">Method kind.</param>
/// <param name="Name">Method name.</param>
/// <param name="Parameters">Parameters.</param>
/// <param name="ReturnType">Declared return type, or null.</param>
/// <param name="Body">The body, or null for an interface member.</param>
public sealed record As3Method(
    SourceSpan Span,
    IReadOnlyList<As3Metadata> Metadata,
    As3Modifier Modifiers,
    As3MethodKind Kind,
    string Name,
    IReadOnlyList<As3Parameter> Parameters,
    As3TypeRef? ReturnType,
    As3Block? Body) : As3Member(Span, Metadata, Modifiers);

/// <summary>A member the parser does not support, such as a namespace or <c>include</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Metadata">Metadata tags.</param>
/// <param name="Modifiers">Modifiers.</param>
/// <param name="Reason">Short name of the construct, such as "namespace".</param>
/// <param name="RawText">The original text.</param>
public sealed record As3UnsupportedMember(
    SourceSpan Span,
    IReadOnlyList<As3Metadata> Metadata,
    As3Modifier Modifiers,
    string Reason,
    string RawText) : As3Member(Span, Metadata, Modifiers);

/// <summary>Base of statements.</summary>
/// <param name="Span">Source range.</param>
public abstract record As3Statement(SourceSpan Span) : As3Node(Span);

/// <summary>A <c>{ ... }</c> block.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Statements">Statements in the block.</param>
public sealed record As3Block(SourceSpan Span, IReadOnlyList<As3Statement> Statements) : As3Statement(Span);

/// <summary>One variable in a <c>var</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">Variable name.</param>
/// <param name="Type">Declared type, or null.</param>
/// <param name="Initializer">Initial value, or null.</param>
public sealed record As3Declarator(SourceSpan Span, string Name, As3TypeRef? Type, As3Expression? Initializer) : As3Node(Span);

/// <summary>A <c>var</c> or <c>const</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="IsConst">True for <c>const</c>.</param>
/// <param name="Declarators">The variables.</param>
public sealed record As3VarStatement(SourceSpan Span, bool IsConst, IReadOnlyList<As3Declarator> Declarators) : As3Statement(Span);

/// <summary>An expression used as a statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Expression">The expression.</param>
public sealed record As3ExpressionStatement(SourceSpan Span, As3Expression Expression) : As3Statement(Span);

/// <summary>An <c>if</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Condition">The condition.</param>
/// <param name="Then">The branch taken when true.</param>
/// <param name="Else">The <c>else</c> branch, or null.</param>
public sealed record As3IfStatement(SourceSpan Span, As3Expression Condition, As3Statement Then, As3Statement? Else) : As3Statement(Span);

/// <summary>A C-style <c>for</c> loop.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Initializer">A var statement or expression statement, or null.</param>
/// <param name="Condition">The loop condition, or null.</param>
/// <param name="Increment">The increment expression, or null.</param>
/// <param name="Body">The loop body.</param>
public sealed record As3ForStatement(SourceSpan Span, As3Statement? Initializer, As3Expression? Condition, As3Expression? Increment, As3Statement Body) : As3Statement(Span);

/// <summary>A <c>for in</c> or <c>for each</c> loop.</summary>
/// <param name="Span">Source range.</param>
/// <param name="IsEach">True for <c>for each</c>.</param>
/// <param name="Variable">The loop variable when declared with <c>var</c>, otherwise null.</param>
/// <param name="Target">The loop variable expression when not declared, otherwise null.</param>
/// <param name="Collection">The collection or object.</param>
/// <param name="Body">The loop body.</param>
public sealed record As3ForInStatement(SourceSpan Span, bool IsEach, As3Declarator? Variable, As3Expression? Target, As3Expression Collection, As3Statement Body) : As3Statement(Span);

/// <summary>A <c>while</c> loop.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Condition">The condition.</param>
/// <param name="Body">The body.</param>
public sealed record As3WhileStatement(SourceSpan Span, As3Expression Condition, As3Statement Body) : As3Statement(Span);

/// <summary>A <c>do ... while</c> loop.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Body">The body.</param>
/// <param name="Condition">The condition.</param>
public sealed record As3DoStatement(SourceSpan Span, As3Statement Body, As3Expression Condition) : As3Statement(Span);

/// <summary>One <c>case</c> or <c>default</c> clause.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Test">The case value, or null for <c>default</c>.</param>
/// <param name="Statements">The statements of the clause.</param>
public sealed record As3SwitchCase(SourceSpan Span, As3Expression? Test, IReadOnlyList<As3Statement> Statements) : As3Node(Span);

/// <summary>A <c>switch</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Discriminant">The switched value.</param>
/// <param name="Cases">The clauses.</param>
public sealed record As3SwitchStatement(SourceSpan Span, As3Expression Discriminant, IReadOnlyList<As3SwitchCase> Cases) : As3Statement(Span);

/// <summary>A <c>catch</c> clause.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">The error variable.</param>
/// <param name="Type">The caught type, or null.</param>
/// <param name="Body">The clause body.</param>
public sealed record As3CatchClause(SourceSpan Span, string Name, As3TypeRef? Type, As3Block Body) : As3Node(Span);

/// <summary>A <c>try</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Body">The protected block.</param>
/// <param name="Catches">The catch clauses.</param>
/// <param name="Finally">The finally block, or null.</param>
public sealed record As3TryStatement(SourceSpan Span, As3Block Body, IReadOnlyList<As3CatchClause> Catches, As3Block? Finally) : As3Statement(Span);

/// <summary>A <c>return</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Value">The returned value, or null.</param>
public sealed record As3ReturnStatement(SourceSpan Span, As3Expression? Value) : As3Statement(Span);

/// <summary>A <c>throw</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Value">The thrown value.</param>
public sealed record As3ThrowStatement(SourceSpan Span, As3Expression Value) : As3Statement(Span);

/// <summary>A <c>break</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Label">The label, or null.</param>
public sealed record As3BreakStatement(SourceSpan Span, string? Label) : As3Statement(Span);

/// <summary>A <c>continue</c> statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Label">The label, or null.</param>
public sealed record As3ContinueStatement(SourceSpan Span, string? Label) : As3Statement(Span);

/// <summary>A labelled statement.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Label">The label.</param>
/// <param name="Body">The labelled statement.</param>
public sealed record As3LabeledStatement(SourceSpan Span, string Label, As3Statement Body) : As3Statement(Span);

/// <summary>An empty statement (a lone semicolon).</summary>
/// <param name="Span">Source range.</param>
public sealed record As3EmptyStatement(SourceSpan Span) : As3Statement(Span);

/// <summary>A statement the parser does not support, such as <c>with</c> or <c>include</c>, or one it could not parse.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Reason">Short name of the construct, such as "with" or "syntax-error".</param>
/// <param name="RawText">The original text.</param>
public sealed record As3UnsupportedStatement(SourceSpan Span, string Reason, string RawText) : As3Statement(Span);

/// <summary>Base of expressions.</summary>
/// <param name="Span">Source range.</param>
public abstract record As3Expression(SourceSpan Span) : As3Node(Span);

/// <summary>What a <see cref="As3Literal"/> holds.</summary>
public enum As3LiteralKind
{
    /// <summary>A number.</summary>
    Number,

    /// <summary>A string. <see cref="As3Literal.Text"/> is the source text with its quotes.</summary>
    String,

    /// <summary>True or false.</summary>
    Boolean,

    /// <summary>The null literal.</summary>
    Null,

    /// <summary>A regular expression. <see cref="As3Literal.Text"/> is the source text.</summary>
    Regex,
}

/// <summary>A literal value.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Kind">The literal kind.</param>
/// <param name="Text">The source text.</param>
public sealed record As3Literal(SourceSpan Span, As3LiteralKind Kind, string Text) : As3Expression(Span);

/// <summary>A name.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">The name. <c>this</c> and <c>super</c> appear here too.</param>
public sealed record As3Identifier(SourceSpan Span, string Name) : As3Expression(Span);

/// <summary>A parenthesized expression.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Inner">The inner expression.</param>
public sealed record As3Parenthesized(SourceSpan Span, As3Expression Inner) : As3Expression(Span);

/// <summary>Member access: <c>target.name</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Target">The object.</param>
/// <param name="Name">The member name.</param>
public sealed record As3MemberAccess(SourceSpan Span, As3Expression Target, string Name) : As3Expression(Span);

/// <summary>Index access: <c>target[index]</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Target">The object.</param>
/// <param name="Index">The index expression.</param>
public sealed record As3IndexAccess(SourceSpan Span, As3Expression Target, As3Expression Index) : As3Expression(Span);

/// <summary>A generic specialization used as an expression: <c>Vector.&lt;int&gt;</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Target">The generic type name.</param>
/// <param name="TypeArguments">The type arguments.</param>
public sealed record As3GenericExpression(SourceSpan Span, As3Expression Target, IReadOnlyList<As3TypeRef> TypeArguments) : As3Expression(Span);

/// <summary>A call.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Target">The callee.</param>
/// <param name="Arguments">The arguments.</param>
public sealed record As3Call(SourceSpan Span, As3Expression Target, IReadOnlyList<As3Expression> Arguments) : As3Expression(Span);

/// <summary>A <c>new</c> expression.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Target">The constructed type expression.</param>
/// <param name="Arguments">The constructor arguments.</param>
public sealed record As3New(SourceSpan Span, As3Expression Target, IReadOnlyList<As3Expression> Arguments) : As3Expression(Span);

/// <summary>A unary or postfix operator.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Operator">The operator text, such as "!" or "typeof".</param>
/// <param name="Operand">The operand.</param>
/// <param name="IsPrefix">False for postfix <c>++</c> and <c>--</c>.</param>
public sealed record As3Unary(SourceSpan Span, string Operator, As3Expression Operand, bool IsPrefix) : As3Expression(Span);

/// <summary>A binary operator, including <c>as</c>, <c>is</c>, <c>in</c> and <c>instanceof</c>.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Operator">The operator text.</param>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand.</param>
public sealed record As3Binary(SourceSpan Span, string Operator, As3Expression Left, As3Expression Right) : As3Expression(Span);

/// <summary>An assignment, including compound assignments.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Operator">"=" or a compound operator such as "+=".</param>
/// <param name="Target">The assigned location.</param>
/// <param name="Value">The assigned value.</param>
public sealed record As3Assignment(SourceSpan Span, string Operator, As3Expression Target, As3Expression Value) : As3Expression(Span);

/// <summary>The <c>a ? b : c</c> operator.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Condition">The condition.</param>
/// <param name="WhenTrue">The value when true.</param>
/// <param name="WhenFalse">The value when false.</param>
public sealed record As3Conditional(SourceSpan Span, As3Expression Condition, As3Expression WhenTrue, As3Expression WhenFalse) : As3Expression(Span);

/// <summary>An array literal.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Elements">The elements.</param>
public sealed record As3ArrayLiteral(SourceSpan Span, IReadOnlyList<As3Expression> Elements) : As3Expression(Span);

/// <summary>One property of an object literal.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Key">The key text without quotes.</param>
/// <param name="Value">The value.</param>
public sealed record As3ObjectProperty(SourceSpan Span, string Key, As3Expression Value) : As3Node(Span);

/// <summary>An object literal.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Properties">The properties.</param>
public sealed record As3ObjectLiteral(SourceSpan Span, IReadOnlyList<As3ObjectProperty> Properties) : As3Expression(Span);

/// <summary>A function expression.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Name">The optional name.</param>
/// <param name="Parameters">Parameters.</param>
/// <param name="ReturnType">Declared return type, or null.</param>
/// <param name="Body">The body.</param>
public sealed record As3FunctionExpression(SourceSpan Span, string? Name, IReadOnlyList<As3Parameter> Parameters, As3TypeRef? ReturnType, As3Block Body) : As3Expression(Span);

/// <summary>An expression the parser does not support, such as E4X or an XML literal.</summary>
/// <param name="Span">Source range.</param>
/// <param name="Reason">Short name of the construct, such as "e4x".</param>
/// <param name="RawText">The original text.</param>
public sealed record As3UnsupportedExpression(SourceSpan Span, string Reason, string RawText) : As3Expression(Span);
