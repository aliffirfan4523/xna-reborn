using System.Collections;
using System.Reflection;

namespace XnaReborn.Flash.Analyzer.As3;

/// <summary>Walks a syntax tree in source order.</summary>
public static class As3Walker
{
    private static readonly Dictionary<Type, PropertyInfo[]> _properties = new();

    /// <summary>Returns <paramref name="root"/> and every node below it, parents before children.</summary>
    /// <param name="root">The first node.</param>
    public static IEnumerable<As3Node> DescendantsAndSelf(As3Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var stack = new Stack<As3Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in Children(node).Reverse())
            {
                stack.Push(child);
            }
        }
    }

    /// <summary>The direct children of a node, in source order.</summary>
    /// <param name="node">The parent.</param>
    // shortcut: reflection over record properties, cached per type; replace with a generated visitor if parsing speed matters.
    public static IEnumerable<As3Node> Children(As3Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        PropertyInfo[] props;
        lock (_properties)
        {
            var type = node.GetType();
            if (!_properties.TryGetValue(type, out props!))
            {
                props = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.Name != "Span" && p.Name != "Diagnostics" && (typeof(As3Node).IsAssignableFrom(p.PropertyType)
                        || (p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType))))
                    .ToArray();
                _properties[type] = props;
            }
        }

        foreach (var p in props)
        {
            var value = p.GetValue(node);
            if (value is As3Node child)
            {
                yield return child;
            }
            else if (value is IEnumerable items and not string)
            {
                foreach (var item in items)
                {
                    if (item is As3Node n)
                    {
                        yield return n;
                    }
                }
            }
        }
    }
}
