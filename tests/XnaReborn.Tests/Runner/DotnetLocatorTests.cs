using System.Runtime.InteropServices;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Processes;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
public sealed class DotnetLocatorTests
{
    private static readonly Func<OSPlatform, bool> _windows = p => p == OSPlatform.Windows;
    private static readonly Func<OSPlatform, bool> _linux = p => p == OSPlatform.Linux;

    [Fact]
    public async Task OnlySdk6_IsNotCompatible()
    {
        var info = await Detect("6.0.428 [C:\\sdk]\n");
        Assert.False(info.SdkCompatible);
        Assert.Equal(["6.0.428"], info.Sdks);
        Assert.Contains("https://dotnet.microsoft.com/download", info.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sdk8_IsCompatible()
    {
        var info = await Detect("6.0.428 [C:\\sdk]\n8.0.404 [C:\\sdk]\n");
        Assert.True(info.SdkCompatible);
        Assert.Equal("8.0.404", info.DotnetVersion);
        Assert.Null(info.Problem);
        Assert.Equal(["6.0.428", "8.0.404"], info.Sdks);
    }

    [Fact]
    public async Task Sdk9Preview_IsCompatibleAndSorted()
    {
        var info = await Detect("9.0.100-rc.1.24452.12 [/usr/share/dotnet/sdk]\n8.0.404 [/usr/share/dotnet/sdk]\n");
        Assert.True(info.SdkCompatible);
        Assert.Equal(["8.0.404", "9.0.100-rc.1.24452.12"], info.Sdks);
    }

    [Fact]
    public async Task EmptySdkList_IsNotCompatible()
    {
        var info = await Detect(string.Empty);
        Assert.False(info.SdkCompatible);
        Assert.Empty(info.Sdks);
        Assert.NotNull(info.Problem);
    }

    [Fact]
    public async Task StartError_ReportsMissingDotnet()
    {
        var fake = new FakeRunner(new ProcessResult(null, false, false, string.Empty, string.Empty, TimeSpan.Zero, "not found"));
        var info = await new DotnetLocator(fake, _windows, "Windows 10").DetectAsync(MonoGameTargetKind.DesktopGL, CancellationToken.None);
        Assert.Null(info.DotnetVersion);
        Assert.False(info.SdkCompatible);
        Assert.Contains("not found", info.Problem, StringComparison.Ordinal);
        Assert.Contains("https://dotnet.microsoft.com/download", info.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MonoGameTargetKind.DesktopGL, true, true)]
    [InlineData(MonoGameTargetKind.DesktopGL, false, true)]
    [InlineData(MonoGameTargetKind.WindowsDX, true, true)]
    [InlineData(MonoGameTargetKind.WindowsDX, false, false)]
    public async Task TargetSupport_Matrix(MonoGameTargetKind target, bool onWindows, bool expected)
    {
        var info = await Detect("8.0.404 [x]\n", target, onWindows ? _windows : _linux);
        Assert.Equal(expected, info.TargetSupported);
        Assert.Equal(expected, info.Problem is null);
        if (!expected)
        {
            Assert.Contains("Windows", info.Problem, StringComparison.Ordinal);
        }
    }

    private static Task<EnvironmentInfo> Detect(string sdkList, MonoGameTargetKind target = MonoGameTargetKind.DesktopGL, Func<OSPlatform, bool>? os = null)
    {
        var fake = new FakeRunner(
            new ProcessResult(0, false, false, "8.0.404\n", string.Empty, TimeSpan.Zero),
            new ProcessResult(0, false, false, sdkList, string.Empty, TimeSpan.Zero));
        return new DotnetLocator(fake, os ?? _windows, "Windows 10.0").DetectAsync(target, CancellationToken.None);
    }

    private sealed class FakeRunner(params ProcessResult[] results) : IProcessRunner
    {
        private int _next;

        public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<OutputLine>? lines, CancellationToken cancellationToken)
        {
            var r = results[Math.Min(_next++, results.Length - 1)];
            return Task.FromResult(r);
        }
    }
}
