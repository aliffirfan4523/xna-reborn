using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Puts a <c>// XNAREBORN:</c> marker above each use of a device-only API that has no desktop stand-in: the media library (XNA056),
/// the phone choosers (XNA059) and third-party device assemblies such as ad controls (XNA063). The code is not changed.
/// </summary>
public sealed class DeviceManualMarkerRewriter : ISourceRewriter
{
    /// <summary>The rule ID of the media library marker.</summary>
    public const string MediaLibraryId = "XNA056";

    /// <summary>The rule ID of the chooser marker.</summary>
    public const string ChooserId = "XNA059";

    /// <summary>The rule ID of the third-party assembly marker.</summary>
    public const string ThirdPartyId = "XNA063";

    private readonly string[] _typeNames;
    private readonly string[] _namespaces;
    private readonly string _message;

    private DeviceManualMarkerRewriter(string ruleId, string[] typeNames, string[] namespaces, string message)
    {
        RuleId = ruleId;
        _typeNames = typeNames;
        _namespaces = namespaces;
        _message = message;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <summary>Marks <c>MediaLibrary</c> and <c>MediaSource</c> uses.</summary>
    public static DeviceManualMarkerRewriter MediaLibrary() => new(
        MediaLibraryId,
        ["MediaLibrary", "MediaSource"],
        ["Microsoft.Xna.Framework.Media"],
        "The device media library has no desktop equivalent. Replace it with your own list of files or remove the feature.");

    /// <summary>Marks the phone choosers.</summary>
    public static DeviceManualMarkerRewriter Choosers() => new(
        ChooserId,
        ["PhotoChooserTask", "CameraCaptureTask", "SaveRingtoneTask"],
        ["Microsoft.Phone.Tasks"],
        "A phone chooser has no desktop equivalent: Show() throws NotSupportedException. Replace it with a file dialog or remove the feature.");

    /// <summary>Marks the usings of advertising assemblies.</summary>
    public static DeviceManualMarkerRewriter ThirdParty() => new(
        ThirdPartyId,
        [],
        ["Microsoft.Advertising", "AdDuplex"],
        "This namespace comes from a device-only assembly that was left out of the migrated project. Remove the code that uses it or replace it with a desktop service.");

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var root = CSharpSyntaxTree.ParseText(context.Text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols)).GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        var positions = new SortedSet<int>();

        if (_typeNames.Length > 0)
        {
            var imported = usings.Any(u => u.Alias is null && _namespaces.Contains(u.Name?.ToString().Trim(), StringComparer.Ordinal));
            foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (_typeNames.Contains(name.Identifier.ValueText, StringComparer.Ordinal)
                    && name.Ancestors().All(a => a is not UsingDirectiveSyntax)
                    && (imported || name.Parent is QualifiedNameSyntax or MemberAccessExpressionSyntax))
                {
                    positions.Add(name.SpanStart);
                }
            }
        }
        else
        {
            foreach (var directive in usings.Where(u => u.Name is { } n && _namespaces.Any(ns => IsNamespaceOrChild(n.ToString().Trim(), ns))))
            {
                positions.Add(directive.SpanStart);
            }
        }

        var text = context.Text;
        var marked = 0;
        foreach (var position in positions.Reverse())
        {
            var next = MarkerInserter.InsertMarker(text, position, RuleId, _message, context.OriginalSymbols);
            if (next != text)
            {
                marked++;
                text = next;
            }
        }

        return marked == 0
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(text, [new RewriteNote(RuleId, $"{marked} use(s) left as they are with a marker. {_message}", NeedsManualWork: true)]);
    }

    private static bool IsNamespaceOrChild(string name, string ns) =>
        name.Equals(ns, StringComparison.Ordinal) || name.StartsWith(ns + ".", StringComparison.Ordinal);
}
