using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>What the analyzer learned from one project file.</summary>
/// <param name="Path">Relative path of the .csproj.</param>
/// <param name="AssemblyName">Assembly name from the project file.</param>
/// <param name="IsXna">True when the project is an XNA project.</param>
/// <param name="XnaVersion">Detected XNA version.</param>
/// <param name="Platform">Target platform.</param>
/// <param name="Profile">Graphics profile, or null when not stated.</param>
/// <param name="TargetFramework">Target framework moniker, or null.</param>
/// <param name="References">Referenced assembly simple names.</param>
/// <param name="CompileFiles">Compile items, relative to the project folder.</param>
/// <param name="DefineConstants">Preprocessor symbols defined by the project.</param>
/// <param name="ContentProjects">Relative paths of nested content projects.</param>
/// <param name="ContentItems">Entries read from the content projects.</param>
/// <param name="PlatformInferred">True when <paramref name="Platform"/> was not stated by XnaPlatform and was guessed from references or defines.</param>
/// <param name="LinkedSourceProjects">Relative paths of the other projects this project links Compile items from, or null when it links none.</param>
public sealed record ProjectInfo(
    string Path,
    string AssemblyName,
    bool IsXna,
    XnaVersion XnaVersion,
    XnaPlatform Platform,
    string? Profile,
    string? TargetFramework,
    IReadOnlyList<string> References,
    IReadOnlyList<string> CompileFiles,
    IReadOnlyList<string> DefineConstants,
    IReadOnlyList<string> ContentProjects,
    IReadOnlyList<ContentItem> ContentItems,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool PlatformInferred = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? LinkedSourceProjects = null);
