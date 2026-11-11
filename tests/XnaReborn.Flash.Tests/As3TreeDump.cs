using System.Collections;
using System.Reflection;
using System.Text;
using XnaReborn.Flash.Analyzer.As3;

namespace XnaReborn.Flash.Tests;

/// <summary>Writes a syntax tree as indented text for snapshots: node names, scalar values and 1-based positions. Offsets are left out so the snapshot stays small.</summary>
public static class As3TreeDump
{
    /// <summary>Dumps a parsed file, then its diagnostics.</summary>
    /// <param name="unit">The tree.</param>
    public static string Dump(As3CompilationUnit unit)
    {
        var sb = new StringBuilder();
        Write(sb, unit, 0);
        foreach (var d in unit.Diagnostics)
        {
            sb.Append("DIAGNOSTIC ").Append(d.Span.Line).Append(':').Append(d.Span.Column).Append(' ').AppendLine(d.Message);
        }

        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value, int depth)
    {
        var indent = new string(' ', depth * 2);
        switch (value)
        {
            case null:
                return;
            case As3Node node:
                sb.Append(indent).Append(node.GetType().Name).Append(" @").Append(node.Span.Line).Append(':').Append(node.Span.Column);
                var children = new List<(string Name, object Value)>();
                foreach (var p in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.Name is "Span" or "EqualityContract" || p.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    var v = p.GetValue(node);
                    if (v is null || (v is IEnumerable seq and not string && !seq.GetEnumerator().MoveNext()))
                    {
                        continue;
                    }

                    if (v is As3Node or (IEnumerable and not string))
                    {
                        children.Add((p.Name, v));
                    }
                    else
                    {
                        sb.Append(' ').Append(p.Name).Append('=').Append(Scalar(v));
                    }
                }

                sb.AppendLine();
                foreach (var (name, child) in children)
                {
                    if (child is As3Node)
                    {
                        sb.Append(indent).Append("  ").Append(name).AppendLine(":");
                        Write(sb, child, depth + 2);
                    }
                    else
                    {
                        sb.Append(indent).Append("  ").Append(name).AppendLine("[]:");
                        foreach (var item in (IEnumerable)child)
                        {
                            if (item is As3Node)
                            {
                                Write(sb, item, depth + 2);
                            }
                            else
                            {
                                sb.Append(indent).Append("    ").AppendLine(Scalar(item));
                            }
                        }
                    }
                }

                break;
            default:
                sb.Append(indent).AppendLine(Scalar(value));
                break;
        }
    }

    private static string Scalar(object? v) => v switch
    {
        string s => "\"" + s.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"",
        null => "null",
        _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
