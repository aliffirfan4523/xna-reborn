using System.Runtime.CompilerServices;

namespace XnaReborn.Tests;

/// <summary>Global Verify settings: snapshots live in <c>tests/XnaReborn.Tests/Snapshots</c>.</summary>
public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init() =>
        Verifier.DerivePathInfo((_, projectDirectory, type, method) =>
            new PathInfo(Path.Combine(projectDirectory, "Snapshots"), type.Name, method.Name));
}
