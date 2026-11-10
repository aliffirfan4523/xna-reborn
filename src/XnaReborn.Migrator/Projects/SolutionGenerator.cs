using System.Security.Cryptography;
using System.Text;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Projects;

/// <summary>Writes a classic Visual Studio 2022 <c>.sln</c> file with deterministic project GUIDs.</summary>
public sealed class SolutionGenerator
{
    private const string _csharpProjectType = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

    /// <summary>Creates the solution file for the given projects.</summary>
    /// <param name="solutionName">File name of the solution without extension.</param>
    /// <param name="projectPaths">Paths of the csproj files relative to the output root, with '/' separators.</param>
    /// <returns>A <see cref="ChangeKind.Created"/> change for <c>{solutionName}.sln</c> with CRLF line endings.</returns>
    /// <exception cref="ArgumentException">The name or the project list is empty.</exception>
    public FileChange Generate(string solutionName, IReadOnlyList<string> projectPaths)
    {
        if (string.IsNullOrWhiteSpace(solutionName))
        {
            throw new ArgumentException("The solution name must not be empty.", nameof(solutionName));
        }

        ArgumentNullException.ThrowIfNull(projectPaths);
        if (projectPaths.Count == 0)
        {
            throw new ArgumentException("The solution needs at least one project.", nameof(projectPaths));
        }

        var projects = projectPaths
            .Select(p => p.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => (Path: p, Name: Path.GetFileNameWithoutExtension(p), Guid: ProjectGuid(p)))
            .ToList();

        var lines = new List<string>
        {
            string.Empty,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            "# Visual Studio Version 17",
            "VisualStudioVersion = 17.0.31903.59",
            "MinimumVisualStudioVersion = 10.0.40219.1",
        };
        foreach (var p in projects)
        {
            lines.Add($"Project(\"{_csharpProjectType}\") = \"{p.Name}\", \"{p.Path.Replace('/', '\\')}\", \"{p.Guid}\"");
            lines.Add("EndProject");
        }

        lines.Add("Global");
        lines.Add("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        lines.Add("\t\tDebug|Any CPU = Debug|Any CPU");
        lines.Add("\t\tRelease|Any CPU = Release|Any CPU");
        lines.Add("\tEndGlobalSection");
        lines.Add("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var p in projects)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                lines.Add($"\t\t{p.Guid}.{config}|Any CPU.ActiveCfg = {config}|Any CPU");
                lines.Add($"\t\t{p.Guid}.{config}|Any CPU.Build.0 = {config}|Any CPU");
            }
        }

        lines.Add("\tEndGlobalSection");
        lines.Add("\tGlobalSection(SolutionProperties) = preSolution");
        lines.Add("\t\tHideSolutionNode = FALSE");
        lines.Add("\tEndGlobalSection");
        lines.Add("EndGlobal");

        // The first line is empty on purpose: Visual Studio writes a blank line before the header.
        var text = string.Join("\r\n", lines) + "\r\n";
        return new FileChange($"{solutionName}.sln", ChangeKind.Created, null, text, null);
    }

    /// <summary>Derives a name-based (SHA-1, version 5 layout) GUID from the lowercase relative path.</summary>
    internal static string ProjectGuid(string relativePath)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/').ToLowerInvariant()));
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexString(bytes);
        return $"{{{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex[20..]}}}";
    }
}
