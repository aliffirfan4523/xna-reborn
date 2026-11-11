namespace XnaReborn.Migrator.Source;

/// <summary>What a preprocessor condition comes to under a <see cref="PlatformSymbolSet"/>.</summary>
/// <param name="Value">True or false when the platform symbols decide the condition, null when another symbol is needed too.</param>
/// <param name="MentionsPlatform">True when the condition names at least one platform symbol.</param>
/// <param name="PlatformOnly">True when every identifier in the condition is a platform symbol (or the condition is true or false).</param>
public sealed record PlatformConditionResult(bool? Value, bool MentionsPlatform, bool PlatformOnly);

/// <summary>
/// Parses and evaluates the condition of an <c>#if</c> or <c>#elif</c> directive with three-valued logic.
/// Supports identifiers, <c>true</c>, <c>false</c>, <c>!</c>, <c>&amp;&amp;</c>, <c>||</c> and parentheses.
/// </summary>
public static class PlatformCondition
{
    /// <summary>Evaluates a condition.</summary>
    /// <param name="condition">The text after <c>#if</c> or <c>#elif</c>, without a trailing comment.</param>
    /// <param name="symbols">The platform symbol set. Platform symbols get its values; other identifiers are unknown.</param>
    /// <returns>The result, or null when the text cannot be parsed (for example it uses <c>==</c>).</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static PlatformConditionResult? Evaluate(string condition, PlatformSymbolSet symbols)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(symbols);

        var parser = new Parser(condition, symbols);
        return parser.Run();
    }

    // Grammar: or := and ('||' and)*; and := not ('&&' not)*; not := '!' not | '(' or ')' | identifier.
    private sealed class Parser(string text, PlatformSymbolSet symbols)
    {
        private int _position;
        private bool _mentions;
        private bool _platformOnly = true;

        public PlatformConditionResult? Run()
        {
            if (!TryOr(out var value) || SkipSpace() != '\0')
            {
                return null;
            }

            return new PlatformConditionResult(value, _mentions, _platformOnly);
        }

        private static bool? Not(bool? value) => value.HasValue ? !value.Value : null;

        private static bool? And(bool? left, bool? right) =>
            left == false || right == false ? false : left == true && right == true ? true : null;

        private static bool? Or(bool? left, bool? right) =>
            left == true || right == true ? true : left == false && right == false ? false : null;

        private char SkipSpace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }

            return _position < text.Length ? text[_position] : '\0';
        }

        private bool Accept(string token)
        {
            SkipSpace();
            if (string.CompareOrdinal(text, _position, token, 0, token.Length) != 0)
            {
                return false;
            }

            _position += token.Length;
            return true;
        }

        private bool TryOr(out bool? value)
        {
            if (!TryAnd(out value))
            {
                return false;
            }

            while (Accept("||"))
            {
                if (!TryAnd(out var right))
                {
                    return false;
                }

                value = Or(value, right);
            }

            return true;
        }

        private bool TryAnd(out bool? value)
        {
            if (!TryNot(out value))
            {
                return false;
            }

            while (Accept("&&"))
            {
                if (!TryNot(out var right))
                {
                    return false;
                }

                value = And(value, right);
            }

            return true;
        }

        private bool TryNot(out bool? value)
        {
            value = null;
            var next = SkipSpace();
            if (next == '!' && !(_position + 1 < text.Length && text[_position + 1] == '='))
            {
                _position++;
                if (!TryNot(out var inner))
                {
                    return false;
                }

                value = Not(inner);
                return true;
            }

            if (next == '(')
            {
                _position++;
                if (!TryOr(out value) || SkipSpace() != ')')
                {
                    return false;
                }

                _position++;
                return true;
            }

            if (!(char.IsAsciiLetter(next) || next == '_'))
            {
                return false;
            }

            var start = _position;
            while (_position < text.Length && (char.IsAsciiLetterOrDigit(text[_position]) || text[_position] == '_'))
            {
                _position++;
            }

            var name = text[start.._position];
            switch (name)
            {
                case "true":
                    value = true;
                    break;
                case "false":
                    value = false;
                    break;
                default:
                    value = symbols.Value(name);
                    _mentions |= value.HasValue;
                    _platformOnly &= value.HasValue;
                    break;
            }

            return true;
        }
    }
}
