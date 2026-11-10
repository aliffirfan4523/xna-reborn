using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Projects;

/// <summary>
/// Writes empty <c>Directory.Build.props</c>, <c>Directory.Build.targets</c> and <c>Directory.Packages.props</c>
/// files at the output root. MSBuild looks upward from a project for each of these and stops at the first one it
/// finds, so the empty files keep a parent folder's build settings and central package management out of the
/// migrated copy.
/// </summary>
public sealed class BuildIsolationGenerator
{
    private const string _comment =
        "  <!-- Stops MSBuild from importing the same file from a parent folder into this migrated project. -->";

    /// <summary>Creates the three isolation files.</summary>
    /// <returns>Three <see cref="ChangeKind.Created"/> changes at the output root, in a fixed order.</returns>
    public IReadOnlyList<FileChange> Generate() =>
    [
        Create("Directory.Build.props", [_comment], "Stops a parent Directory.Build.props from changing how the migrated game builds."),
        Create("Directory.Build.targets", [_comment], "Stops a parent Directory.Build.targets from changing how the migrated game builds."),
        Create(
            "Directory.Packages.props",
            [
                _comment,
                "  <PropertyGroup>",
                "    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>",
                "  </PropertyGroup>",
            ],
            "Turns off central package management inherited from a parent folder (NU1008)."),
    ];

    private static FileChange Create(string path, string[] body, string reason) =>
        new(path, ChangeKind.Created, null, "<Project>\n" + string.Join("\n", body) + "\n</Project>\n", reason);
}
