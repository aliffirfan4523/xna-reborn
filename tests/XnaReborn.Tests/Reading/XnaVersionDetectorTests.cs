using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;

namespace XnaReborn.Tests.Reading;

public sealed class XnaVersionDetectorTests
{
    private const string _projectPath = @"Game\Game.csproj";
    private const string _import40 = @"$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets";
    private const string _import31 = @"$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.targets";
    private const string _reference31 = "Microsoft.Xna.Framework, Version=3.1.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553";
    private const string _reference40 = "Microsoft.Xna.Framework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553";

    [Theory]
    [InlineData("v4.0", XnaVersion.V40)]
    [InlineData("v3.1", XnaVersion.V31)]
    [InlineData("v3.0", XnaVersion.V30)]
    [InlineData("4.0", XnaVersion.V40)]
    public void PropertyAloneDetectsVersion(string propertyValue, XnaVersion expected)
    {
        var result = XnaVersionDetector.Detect(Props(propertyValue), NoItems(), _projectPath);

        Assert.Equal(expected, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void ImportAloneDetectsV40FromTargetsPath()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Import(_import40)], _projectPath);

        Assert.Equal(XnaVersion.V40, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void ImportAloneDetectsV31FromTargetsPath()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Import(_import31)], _projectPath);

        Assert.Equal(XnaVersion.V31, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void ImportWithoutXnaGameStudioIsIgnored()
    {
        const string otherImport = @"$(MSBuildToolsPath)\Microsoft.CSharp.targets";

        var result = XnaVersionDetector.Detect(NoProps(), [Import(otherImport)], _projectPath);

        Assert.Equal(XnaVersion.Unknown, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void ReferenceAloneDetectsV31FromFrameworkVersion()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Reference(_reference31)], _projectPath);

        Assert.Equal(XnaVersion.V31, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void ReferenceAloneDetectsV40FromFrameworkVersion()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Reference(_reference40)], _projectPath);

        Assert.Equal(XnaVersion.V40, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void ReferenceToOtherAssemblyWithVersion4IsIgnored()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Reference("System.Core, Version=4.0.0.0, Culture=neutral")], _projectPath);

        Assert.Equal(XnaVersion.Unknown, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void NoSignalReturnsUnknownWithoutConflict()
    {
        var result = XnaVersionDetector.Detect(NoProps(), NoItems(), _projectPath);

        Assert.Equal(XnaVersion.Unknown, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void UnsupportedPropertyValueReturnsUnknownWithoutGuessing()
    {
        var result = XnaVersionDetector.Detect(Props("v2.0"), NoItems(), _projectPath);

        Assert.Equal(XnaVersion.Unknown, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void AllThreeSignalsAgreeReturnsThatVersionWithoutConflict()
    {
        var result = XnaVersionDetector.Detect(
            Props("v4.0"),
            [Import(_import40), Reference(_reference40)],
            _projectPath);

        Assert.Equal(XnaVersion.V40, result.Version);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void PropertyBeatsDisagreeingImportAndRaisesAna002()
    {
        var result = XnaVersionDetector.Detect(Props("v4.0"), [Import(_import31)], _projectPath);

        Assert.Equal(XnaVersion.V40, result.Version);
        Assert.NotNull(result.Conflict);
        Assert.Equal("ANA002", result.Conflict.Code);
        Assert.Equal(_projectPath, result.Conflict.File);
        Assert.Contains("4.0", result.Conflict.Message, StringComparison.Ordinal);
        Assert.Contains("3.1", result.Conflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportBeatsDisagreeingReferenceAndRaisesAna002()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Import(_import31), Reference(_reference40)], _projectPath);

        Assert.Equal(XnaVersion.V31, result.Version);
        Assert.NotNull(result.Conflict);
        Assert.Equal("ANA002", result.Conflict.Code);
    }

    [Fact]
    public void DisagreeingReferencesWithNoOtherSignalUseFirstReferenceAndRaiseAna002()
    {
        var result = XnaVersionDetector.Detect(NoProps(), [Reference(_reference31), Reference(_reference40)], _projectPath);

        Assert.Equal(XnaVersion.V31, result.Version);
        Assert.NotNull(result.Conflict);
        Assert.Equal("ANA002", result.Conflict.Code);
    }

    private static IReadOnlyDictionary<string, string> NoProps() => new Dictionary<string, string>();

    private static IReadOnlyDictionary<string, string> Props(string xnaFrameworkVersion) =>
        new Dictionary<string, string> { ["XnaFrameworkVersion"] = xnaFrameworkVersion };

    private static IReadOnlyList<ProjectItem> NoItems() => [];

    private static ProjectItem Import(string project) =>
        new("Import", project, new Dictionary<string, string>());

    private static ProjectItem Reference(string include) =>
        new("Reference", include, new Dictionary<string, string>());
}
