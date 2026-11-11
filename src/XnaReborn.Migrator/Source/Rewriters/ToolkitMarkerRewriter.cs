using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Puts one <c>// XNAREBORN: SDXnnn:</c> marker above the first use in a file of a Toolkit API that has no MonoGame form (Manual and
/// Unsupported rules). The code is not changed, so the compiler errors that follow point at the marked line. Matching is by syntax,
/// the way the analyzer matches, but it cannot require the Toolkit using directives, because the namespace rewriters have already changed them.
/// </summary>
public sealed class ToolkitMarkerRewriter : ISourceRewriter
{
    private readonly string[] _names;
    private readonly string? _receiver;
    private readonly bool _member;
    private readonly bool _invocation;
    private readonly int _minArguments;
    private readonly int _maxArguments;
    private readonly string _message;

    private ToolkitMarkerRewriter(string ruleId, string[] names, string? receiver, bool member, bool invocation, int minArguments, int maxArguments, string message)
    {
        RuleId = ruleId;
        _names = names;
        _receiver = receiver;
        _member = member;
        _invocation = invocation;
        _minArguments = minArguments;
        _maxArguments = maxArguments;
        _message = message;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <summary>Creates every marker rewriter of the Toolkit migration, in rule ID order.</summary>
    public static IReadOnlyList<ISourceRewriter> CreateAll() =>
    [
        new NamespaceMarkerRewriter("SDX005", ["SharpDX.Direct3D11"], "Raw Direct3D 11 has no MonoGame equivalent. Rewrite this code against the MonoGame graphics API."),
        new NamespaceMarkerRewriter("SDX006", ["SharpDX.DXGI"], "DXGI formats and settings have no name-for-name MonoGame form. Use SurfaceFormat and PresentationParameters."),
        new NamespaceMarkerRewriter("SDX007", ["SharpDX.Direct2D1"], "Direct2D has no MonoGame equivalent. Redraw this content with SpriteBatch or a render target."),
        new NamespaceMarkerRewriter("SDX008", ["SharpDX.XAudio2"], "XAudio2 has no MonoGame equivalent. Use SoundEffect and SoundEffectInstance."),
        ForTypes("SDX026", ["MouseManager"], "MonoGame has the static Mouse class. Replace the manager with Mouse.GetState() and check the coordinates."),
        ForTypes("SDX027", ["PointerManager"], "MonoGame uses TouchPanel with a different event model. Port this touch code by hand."),
        ForCalls("SDX028", ["IsKeyPressed", "IsKeyReleased"], null, false, 0, int.MaxValue, "MonoGame does not track key edges. Keep the previous KeyboardState and compare it with the current one."),
        ForCalls("SDX032", ["LookAtLH", "PerspectiveFovLH", "OrthoLH", "OrthoOffCenterLH"], "Matrix", true, 0, int.MaxValue, "XNA and MonoGame are right-handed. Convert the scene to right-handed coordinates, then use the Matrix.Create* factories."),
        ForCalls("SDX033", ["TransformCoordinate"], null, true, 0, int.MaxValue, "TransformCoordinate divides by w; Vector3.Transform does not. Use Vector3.Transform for affine matrices."),
        ForTypes("SDX034", ["RectangleF"], "MonoGame has no RectangleF. Use Rectangle, or a small struct of your own."),
        ForTypes("SDX035", ["Color4", "Color3"], "MonoGame has no Color4 or Color3. Use Color for bytes or Vector4 for floats."),
        ForCalls("SDX043", ["SetRenderTargets"], "GraphicsDevice", true, 2, 8, "In MonoGame a render target owns its depth buffer. Create the targets with the depth format they need and use SetRenderTargets(RenderTargetBinding[])."),
        ForCalls("SDX045", ["New"], "RenderTarget2D", true, 0, int.MaxValue, "RenderTarget2D.New has no MonoGame form. Use new RenderTarget2D(device, width, height, mipMap, SurfaceFormat, DepthFormat)."),
        ForCalls("SDX046", ["New"], "Texture2D", true, 0, int.MaxValue, "Texture2D.New has no MonoGame form. Use new Texture2D(device, width, height) and SetData."),
        ForTypes("SDX047", ["Model"], "The Toolkit Model draws itself with Model.Draw(device, world, view, projection). MonoGame draws mesh by mesh with each effect."),
        ForTypes("SDX048", ["GeometricPrimitive"], "MonoGame has no GeometricPrimitive. Build the vertex and index buffers yourself, or load a model."),
        ForCalls("SDX049", ["SetResource"], null, true, 0, int.MaxValue, "Use EffectParameter.SetValue(texture) and set samplers on the device."),
        ForCalls("SDX050", ["DrawQuad"], null, true, 0, int.MaxValue, "MonoGame has no DrawQuad. Draw a full-screen SpriteBatch pass instead."),
        ForCalls("SDX051", ["Begin"], "spriteBatch", true, 2, 2, "If this is Begin(SpriteSortMode, Effect), MonoGame has no such overload. Pass the effect in the long overload."),
        ForProperty("SDX061", "PreferredGraphicsProfile", "MonoGame has GraphicsProfile.Reach and HiDef. Set GraphicsDeviceManager.GraphicsProfile."),
        ForTypes("SDX062", ["GameSystem", "GameSystemCollection"], "Port each system to GameComponent or DrawableGameComponent."),
        ForTypes("SDX063", ["SharpDXElement", "GameContext"], "A MonoGame desktop game owns its window. Move the surrounding UI out of the game."),
    ];

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var root = ToolkitSyntax.Parse(context.Text, context.OriginalSymbols);
        if (!ToolkitSyntax.HasFrameworkContext(root))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var position = FirstMatch(root);
        if (position is null)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var marked = MarkerInserter.InsertMarker(context.Text, position.Value, RuleId, _message, context.OriginalSymbols);
        return string.Equals(marked, context.Text, StringComparison.Ordinal)
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(marked, [new RewriteNote(RuleId, $"Left a use unchanged and added a marker: {_message}", NeedsManualWork: true)]);
    }

    private static ToolkitMarkerRewriter ForTypes(string ruleId, string[] names, string message) =>
        new(ruleId, names, null, member: false, invocation: false, 0, int.MaxValue, message);

    private static ToolkitMarkerRewriter ForCalls(string ruleId, string[] names, string? receiver, bool invocation, int min, int max, string message) =>
        new(ruleId, names, receiver, member: true, invocation, min, max, message);

    private static ToolkitMarkerRewriter ForProperty(string ruleId, string name, string message) =>
        new(ruleId, [name], null, member: true, invocation: false, 0, int.MaxValue, message);

    private int? FirstMatch(SyntaxNode root)
    {
        var best = int.MaxValue;
        foreach (var node in root.DescendantNodes())
        {
            var position = _member ? MatchMember(node) : MatchType(node);
            if (position is { } found && found < best)
            {
                best = found;
            }
        }

        return best == int.MaxValue ? null : best;
    }

    private int? MatchMember(SyntaxNode node)
    {
        if (node is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax name
            || !_names.Contains(name.Identifier.ValueText, StringComparer.Ordinal))
        {
            return null;
        }

        if (_receiver is not null && !string.Equals(ToolkitSyntax.ReceiverName(access.Expression), _receiver, StringComparison.Ordinal))
        {
            return null;
        }

        var call = access.Parent is InvocationExpressionSyntax invocation && invocation.Expression == access ? invocation : null;
        if (_invocation && call is null)
        {
            return null;
        }

        if (_maxArguments != int.MaxValue || _minArguments > 0)
        {
            if (call is null)
            {
                return null;
            }

            var count = call.ArgumentList.Arguments.Count;
            if (count < _minArguments || count > _maxArguments)
            {
                return null;
            }
        }

        return access.SpanStart;
    }

    private int? MatchType(SyntaxNode node)
    {
        if (node is not SimpleNameSyntax name
            || !_names.Contains(name.Identifier.ValueText, StringComparer.Ordinal)
            || name.Ancestors().Any(a => a is UsingDirectiveSyntax))
        {
            return null;
        }

        // `owner.Model` is a member of another object. `SharpDX.Model` is the type with its namespace in front.
        if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name && !access.Expression.ToString().StartsWith("SharpDX", StringComparison.Ordinal))
        {
            return null;
        }

        return name.SpanStart;
    }
}
