using System.Text.RegularExpressions;

namespace XnaReborn.Tests.Fixtures;

/// <summary>Task 2.3: every MonoGame upgrade fixture is listed in <c>tests/fixtures/README.md</c> with the rules it is positive for.</summary>
public sealed partial class MonoGameFixtureTableTests
{
    [GeneratedRegex(@"^\|\s*`(?<name>[^`]+)`\s*\|[^|]*\|(?<rules>[^|]*)\|\s*$", RegexOptions.Multiline)]
    private static partial Regex TableRow();

    [GeneratedRegex(@"MG\d{3}")]
    private static partial Regex RuleId();

    public static IEnumerable<object[]> NameRows() => FixturePaths.MonoGameNameRows();

    private static Dictionary<string, string[]> ReadTable()
    {
        var text = File.ReadAllText(Path.Combine(FixturePaths.Root, "README.md"));
        return TableRow().Matches(text).ToDictionary(
            m => m.Groups["name"].Value,
            m => RuleId().Matches(m.Groups["rules"].Value).Select(r => r.Value).ToArray());
    }

    [Theory]
    [MemberData(nameof(NameRows))]
    public void Fixture_is_listed_with_the_rules_it_is_positive_for(string name)
    {
        var table = ReadTable();

        Assert.True(table.ContainsKey(name), $"{name} is missing from tests/fixtures/README.md");
        Assert.Equal(FixturePaths.MonoGamePositives[name].Order(StringComparer.Ordinal), table[name].Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_fixture_folder_on_disk_is_a_known_fixture()
    {
        var onDisk = Directory.GetDirectories(FixturePaths.Root).Select(d => Path.GetFileName(d)!).Where(n => n != "flash");

        Assert.All(onDisk, name => Assert.True(
            FixturePaths.AllNames.Contains(name) || FixturePaths.MonoGameNames.Contains(name) || FixturePaths.SharpDxNames.Contains(name),
            $"{name} is not in FixturePaths"));
    }

    [Fact]
    public void Protobuild_placeholder_is_text_and_not_executable_content()
    {
        var path = Path.Combine(FixturePaths.Of("mg3x-protobuild"), "Protobuild.exe");

        var bytes = File.ReadAllBytes(path);

        Assert.DoesNotContain((byte)0, bytes);
        Assert.False(bytes.Length >= 2 && bytes[0] == (byte)'M' && bytes[1] == (byte)'Z', "Protobuild.exe must be a text placeholder.");
    }
}
