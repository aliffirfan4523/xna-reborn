namespace XnaReborn.Analyzer.Reading;

/// <summary>One item from a project file, kept raw so rules can match on it.</summary>
/// <param name="ItemType">Element name, such as Reference, Compile, NestedContentProject, or Import.</param>
/// <param name="Include">The Include attribute. For Import, the Project attribute.</param>
/// <param name="Metadata">Child elements by name (for example Name, Importer, HintPath). Last value wins.</param>
public sealed record ProjectItem(string ItemType, string Include, IReadOnlyDictionary<string, string> Metadata);
