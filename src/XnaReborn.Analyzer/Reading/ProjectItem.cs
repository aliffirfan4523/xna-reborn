namespace XnaReborn.Analyzer.Reading;

/// <summary>One item from a project file, kept raw so rules can match on it.</summary>
/// <param name="ItemType">Element name, such as Reference, Compile, NestedContentProject, or Import.</param>
/// <param name="Include">The Include attribute. For Import, the Project attribute.</param>
/// <param name="Metadata">Attributes other than Include and child elements, by name (for example Version, Name, Importer, HintPath). A child element wins over an attribute of the same name; the last child wins.</param>
public sealed record ProjectItem(string ItemType, string Include, IReadOnlyDictionary<string, string> Metadata);
