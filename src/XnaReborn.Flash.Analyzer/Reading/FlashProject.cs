using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer.As3;

namespace XnaReborn.Flash.Analyzer.Reading;

/// <summary>One parsed ActionScript file of a Flash project.</summary>
/// <param name="Path">Path relative to the project root, with forward slashes.</param>
/// <param name="Text">The file text.</param>
/// <param name="Unit">The syntax tree.</param>
/// <param name="IsFlixelSource">True for a bundled copy of Flixel itself (package org.flixel). It is not converted.</param>
public sealed record FlashSourceFile(string Path, string Text, As3CompilationUnit Unit, bool IsFlixelSource);

/// <summary>A class that extends <c>FlxGame</c>.</summary>
/// <param name="Name">The class name.</param>
/// <param name="FullName">Package-qualified name.</param>
/// <param name="File">Relative path of the file.</param>
/// <param name="Metadata">Metadata tags on the class, such as SWF and Frame.</param>
public sealed record FlashEntryClass(string Name, string FullName, string File, IReadOnlyList<As3Metadata> Metadata);

/// <summary>Everything the analyzer and converter need to know about a Flash project.</summary>
/// <param name="Root">Absolute project folder.</param>
/// <param name="ProjectFile">Relative path of the project file, or null.</param>
/// <param name="SourceRoots">Relative source folders ("." for the project folder).</param>
/// <param name="Files">Parsed files, sorted by path.</param>
/// <param name="EntryClasses">Classes that extend FlxGame in project code.</param>
/// <param name="UsesFlixel">True when any file imports or declares org.flixel.</param>
/// <param name="BundlesFlixel">True when the Flixel source is part of the project.</param>
/// <param name="FlixelVersion">Version such as "2.55" read from a bundled Flixel, or null when unknown.</param>
/// <param name="Detection">The file-name based detection result.</param>
/// <param name="Diagnostics">Problems met while reading.</param>
public sealed record FlashProject(
    string Root,
    string? ProjectFile,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<FlashSourceFile> Files,
    IReadOnlyList<FlashEntryClass> EntryClasses,
    bool UsesFlixel,
    bool BundlesFlixel,
    string? FlixelVersion,
    Detection.FlashDetection Detection,
    IReadOnlyList<Diagnostic> Diagnostics);
