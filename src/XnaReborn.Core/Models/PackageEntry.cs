namespace XnaReborn.Core.Models;

/// <summary>One entry of a <c>packages.config</c> file.</summary>
/// <param name="Id">NuGet package id.</param>
/// <param name="Version">Package version as written.</param>
/// <param name="TargetFramework">The <c>targetFramework</c> attribute (for example "net47"), or null.</param>
/// <param name="DevelopmentDependency">True when the entry is marked <c>developmentDependency</c>.</param>
public sealed record PackageEntry(string Id, string Version, string? TargetFramework, bool DevelopmentDependency = false);
