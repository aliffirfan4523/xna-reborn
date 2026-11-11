using System.Text;
using System.Text.RegularExpressions;
using XnaReborn.Core.Content;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Effects;

/// <summary>
/// Replaces fixed shader models in <c>compile</c> statements with macros that pick a model per target:
/// the original on OpenGL, a Shader Model 4 level 9 profile on DirectX. Idempotent, and everything else in the file stays byte for byte.
/// </summary>
public static partial class FxProfileRewriter
{
    private const string _blockComment = "// XNA Reborn: shader models set per target (OpenGL keeps the original; DirectX needs Shader Model 4 level 9).";

    [GeneratedRegex(@"^(vs|ps)_\d_[0-4ab]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Literal();

    /// <summary>Rewrites the models of one effect.</summary>
    /// <param name="text">The effect text.</param>
    /// <param name="profiles">The profile data. Defaults to <see cref="EffectProfiles.Default"/>.</param>
    public static FxRewriteResult Rewrite(string text, EffectProfiles? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        profiles ??= EffectProfiles.Default;
        var document = FxScanner.Scan(text);
        if (document.Compiles.Count == 0)
        {
            return FxRewriteResult.Unchanged(text);
        }

        var notes = new List<RewriteNote>();
        var edits = new List<FxEdit>();
        var macros = new List<(string Name, EffectProfile Profile)>();
        var removedDefines = new List<FxDefine>();

        var literalModels = document.Compiles
            .Where(c => Literal().IsMatch(c.Model))
            .Select(c => c.Model.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var vertex = literalModels.Where(m => m.StartsWith("vs", StringComparison.Ordinal)).ToList();
        var pixel = literalModels.Where(m => m.StartsWith("ps", StringComparison.Ordinal)).ToList();
        var single = vertex.Count <= 1 && pixel.Count <= 1;

        foreach (var compile in document.Compiles)
        {
            if (Literal().IsMatch(compile.Model))
            {
                var model = compile.Model.ToLowerInvariant();
                if (!profiles.TryGet(model, out var profile))
                {
                    notes.Add(new RewriteNote(FxRuleIds.Profile, $"Line {compile.Line}: shader model '{compile.Model}' is not in the profile table, so it was left as written.", true));
                    continue;
                }

                var name = single
                    ? (model.StartsWith("vs", StringComparison.Ordinal) ? "VS_SHADERMODEL" : "PS_SHADERMODEL")
                    : "XR_" + model.ToUpperInvariant();
                edits.Add(new FxEdit(compile.ModelStart, compile.ModelLength, name));
                if (!macros.Any(m => m.Name == name))
                {
                    macros.Add((name, profile));
                }

                continue;
            }

            var defined = document.Defines.Where(d => d.Name == compile.Model).ToList();
            if (defined.Count == 0 || defined.Any(d => d.ConditionalDepth > 0))
            {
                continue; // already portable, or the macro comes from somewhere the scanner cannot see
            }

            var value = defined[^1].Value.ToLowerInvariant();
            if (Literal().IsMatch(value) && profiles.TryGet(value, out var macroProfile))
            {
                if (!macros.Any(m => m.Name == compile.Model))
                {
                    macros.Add((compile.Model, macroProfile));
                    removedDefines.AddRange(defined);
                }
            }
            else
            {
                notes.Add(new RewriteNote(FxRuleIds.Profile, $"Line {compile.Line}: macro '{compile.Model}' is defined as '{defined[^1].Value}', which is not in the profile table, so it was left as written.", true));
            }
        }

        if (macros.Count == 0)
        {
            return new FxRewriteResult(text, notes);
        }

        var nl = document.NewLine;
        var block = BuildBlock(macros, nl);
        var insertAt = removedDefines.Count > 0 ? removedDefines.Min(d => d.Start) : document.FirstCodeOffset;
        foreach (var define in removedDefines)
        {
            var end = define.Start + define.Length;
            var lineEnd = end;
            if (lineEnd < text.Length && text[lineEnd] == '\r')
            {
                lineEnd++;
            }

            if (lineEnd < text.Length && text[lineEnd] == '\n')
            {
                lineEnd++;
            }

            edits.Add(new FxEdit(define.Start, lineEnd - define.Start, define.Start == insertAt ? block : string.Empty));
        }

        if (removedDefines.Count == 0)
        {
            edits.Add(new FxEdit(insertAt, 0, block + nl));
        }

        var rewritten = FxEdits.Apply(text, edits);
        notes.Insert(0, new RewriteNote(
            FxRuleIds.Profile,
            "Shader models were replaced by per-target macros: " + string.Join(", ", macros.Select(m => $"{m.Name} ({m.Profile.OpenGl} on OpenGL, {m.Profile.DirectX} on DirectX)")) + ". Check the rendered result.",
            false));
        return new FxRewriteResult(rewritten, notes);
    }

    private static string BuildBlock(List<(string Name, EffectProfile Profile)> macros, string nl)
    {
        var sb = new StringBuilder();
        sb.Append(_blockComment).Append(nl);
        sb.Append("#if OPENGL").Append(nl);
        foreach (var (name, profile) in macros)
        {
            sb.Append("    #define ").Append(name).Append(' ').Append(profile.OpenGl).Append(nl);
        }

        sb.Append("#else").Append(nl);
        foreach (var (name, profile) in macros)
        {
            sb.Append("    #define ").Append(name).Append(' ').Append(profile.DirectX).Append(nl);
        }

        sb.Append("#endif").Append(nl);
        return sb.ToString();
    }
}
