using System.Security.Cryptography;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Tests.Migrator;

/// <summary>Task 2: the output guard never lets a migration touch the source or silently replace an output folder.</summary>
[Trait("Category", "Safety")]
public sealed class OutputGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xr-guard-" + Guid.NewGuid().ToString("N"));
    private readonly string _source;
    private readonly OutputGuard _guard = new();

    public OutputGuardTests()
    {
        _source = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(_source, "sub"));
        File.WriteAllText(Path.Combine(_source, "Game.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_source, "sub", "Program.cs"), "class P {}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string P(string name) => Path.Combine(_root, name);

    private static string Snapshot(string folder)
    {
        var lines = Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories)
            .Select(e => Path.GetRelativePath(folder, e) + (File.Exists(e) ? ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(e))) : "/"))
            .Order(StringComparer.Ordinal);
        return string.Join("\n", lines);
    }

    private static void Fill(string folder, string file, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, file), text);
    }

    [Fact]
    public void Null_and_empty_paths_are_invalid()
    {
        Assert.Equal(GuardProblem.InvalidPath, _guard.Check(_source, "", false).Problem);
        Assert.Equal(GuardProblem.InvalidPath, _guard.Check(_source, null!, false).Problem);
        Assert.Equal(GuardProblem.InvalidPath, _guard.Check("  ", P("out"), false).Problem);
        Assert.Equal(GuardProblem.InvalidPath, _guard.Check(_source, "bad\0name", false).Problem);
    }

    [Fact]
    public void A_drive_root_is_not_a_valid_output()
    {
        var root = Path.GetPathRoot(_root)!;

        var result = _guard.Check(_source, root, true);

        Assert.False(result.Allowed);
        Assert.Equal(GuardProblem.InvalidPath, result.Problem);
    }

    [Fact]
    public void A_missing_source_is_refused()
    {
        var result = _guard.Check(P("nope"), P("out"), false);

        Assert.Equal(GuardProblem.SourceMissing, result.Problem);
        Assert.False(result.Allowed);
        Assert.False(Directory.Exists(P("out")));
    }

    [Fact]
    public void Output_equal_to_source_is_refused_even_when_confirmed()
    {
        var result = _guard.Check(_source, _source, true);

        Assert.Equal(GuardProblem.OutputIsSource, result.Problem);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Output_inside_source_is_refused()
    {
        Assert.Equal(GuardProblem.OutputInsideSource, _guard.Check(_source, Path.Combine(_source, "out"), true).Problem);
        Assert.Equal(GuardProblem.OutputInsideSource, _guard.Check(_source, Path.Combine(_source, "sub"), true).Problem);
    }

    [Fact]
    public void Source_inside_output_is_refused()
    {
        Assert.Equal(GuardProblem.SourceInsideOutput, _guard.Check(_source, _root, true).Problem);
    }

    [Fact]
    public void Output_that_is_a_file_is_refused()
    {
        File.WriteAllText(P("out"), "x");

        Assert.Equal(GuardProblem.OutputIsAFile, _guard.Check(_source, P("out"), true).Problem);
    }

    [Fact]
    public void Non_empty_output_needs_confirmation()
    {
        Fill(P("out"), "keep.txt", "mine");

        var result = _guard.Check(_source, P("out"), false);

        Assert.Equal(GuardProblem.OutputNotEmpty, result.Problem);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void Missing_empty_and_confirmed_non_empty_outputs_are_allowed()
    {
        Assert.True(_guard.Check(_source, P("missing"), false).Allowed);
        Assert.Equal(GuardProblem.None, _guard.Check(_source, P("missing"), false).Problem);

        Directory.CreateDirectory(P("empty"));
        Assert.True(_guard.Check(_source, P("empty"), false).Allowed);

        Fill(P("full"), "a.txt", "a");
        Assert.True(_guard.Check(_source, P("full"), true).Allowed);
    }

    [Fact]
    public void Trailing_and_mixed_separators_are_normalized()
    {
        var sep = Path.DirectorySeparatorChar;
        var alt = Path.AltDirectorySeparatorChar;

        Assert.Equal(GuardProblem.OutputIsSource, _guard.Check(_source, _source + sep, true).Problem);
        Assert.Equal(GuardProblem.OutputIsSource, _guard.Check(_source + sep + sep, _source + alt, true).Problem);
        Assert.Equal(GuardProblem.OutputInsideSource, _guard.Check(_source, _source.Replace(sep, alt) + alt + "out" + alt, true).Problem);
    }

    [Fact]
    public void Dot_segments_are_resolved()
    {
        Assert.Equal(GuardProblem.OutputIsSource, _guard.Check(_source, Path.Combine(_root, "other", "..", "src"), true).Problem);
        Assert.Equal(GuardProblem.OutputInsideSource, _guard.Check(_source, Path.Combine(_source, "sub", "..", "x"), true).Problem);
        Assert.Equal(GuardProblem.OutputIsSource, _guard.Check(_source, Path.Combine(_root, ".", "src"), true).Problem);
        Assert.True(_guard.Check(_source, Path.Combine(_source, "..", "fresh"), false).Allowed);
    }

    [Fact]
    public void Case_differences_follow_the_platform()
    {
        var upper = Path.Combine(_root, "SRC");

        var result = _guard.Check(_source, upper, true);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(GuardProblem.OutputIsSource, result.Problem);
        }
        else
        {
            Assert.True(result.Allowed);
        }
    }

    [Fact]
    public void A_sibling_with_a_similar_prefix_is_not_inside_the_source()
    {
        Assert.True(_guard.Check(_source, P("src2"), false).Allowed);

        Directory.CreateDirectory(P("src2"));
        Assert.True(_guard.Check(_source, P("src2"), false).Allowed);
        Assert.True(_guard.Check(P("src2"), _source, true).Allowed);
    }

    [Fact]
    public void Check_creates_and_deletes_nothing()
    {
        var before = Snapshot(_root);

        _guard.Check(_source, P("out"), false);
        _guard.Check(_source, _source, false);

        Assert.Equal(before, Snapshot(_root));
    }

    [Fact]
    public void Begin_throws_with_the_result_when_refused()
    {
        var ex = Assert.Throws<OutputGuardException>(() => _guard.Begin(_source, Path.Combine(_source, "out"), true));

        Assert.Equal(GuardProblem.OutputInsideSource, ex.Result.Problem);
        Assert.Equal(ex.Result.Message, ex.Message);
        Assert.False(Directory.Exists(Path.Combine(_source, "out")));
    }

    [Fact]
    public void Begin_creates_a_staging_sibling_with_a_marker_and_nothing_in_the_source()
    {
        var before = Snapshot(_source);

        using var session = _guard.Begin(_source, P("deep/er/out"), false);

        Assert.Equal(P("deep/er/out.xnareborn-incomplete").Replace('/', Path.DirectorySeparatorChar), session.StagingFolder);
        Assert.Equal(Path.GetFullPath(P("deep/er/out")), session.OutputFolder);
        Assert.True(File.Exists(Path.Combine(session.StagingFolder, "INCOMPLETE.txt")));
        Assert.False(Directory.Exists(session.OutputFolder));
        Assert.False(session.Committed);
        Assert.Null(session.BackupFolder);
        Assert.Equal(before, Snapshot(_source));
    }

    [Fact]
    public void A_staging_name_clash_gets_a_numeric_suffix()
    {
        Directory.CreateDirectory(P("out.xnareborn-incomplete"));

        using var first = _guard.Begin(_source, P("out"), false);
        using var second = _guard.Begin(_source, P("out"), false);

        Assert.Equal(P("out.xnareborn-incomplete-2"), first.StagingFolder);
        Assert.Equal(P("out.xnareborn-incomplete-3"), second.StagingFolder);
        Assert.Empty(Directory.EnumerateFileSystemEntries(P("out.xnareborn-incomplete")));
    }

    [Fact]
    public void Commit_into_a_missing_output_moves_staging_into_place()
    {
        using var session = _guard.Begin(_source, P("out"), false);
        Fill(session.StagingFolder, "new.txt", "new");

        session.Commit();

        Assert.True(session.Committed);
        Assert.Null(session.BackupFolder);
        Assert.Equal("new", File.ReadAllText(P("out/new.txt")));
        Assert.False(File.Exists(P("out/INCOMPLETE.txt")));
        Assert.False(Directory.Exists(session.StagingFolder));
    }

    [Fact]
    public void Commit_over_an_empty_output_replaces_it_without_a_backup()
    {
        Directory.CreateDirectory(P("out"));
        using var session = _guard.Begin(_source, P("out"), false);
        Fill(session.StagingFolder, "new.txt", "new");

        session.Commit();

        Assert.Null(session.BackupFolder);
        Assert.Equal("new", File.ReadAllText(P("out/new.txt")));
        Assert.Empty(Directory.GetDirectories(_root, "out.backup-*"));
    }

    [Fact]
    public void Commit_over_a_confirmed_non_empty_output_keeps_the_old_content_in_a_backup()
    {
        Fill(P("out"), "old.txt", "old");
        Fill(Path.Combine(P("out"), "dir"), "deep.txt", "deep");
        var oldSnapshot = Snapshot(P("out"));
        using var session = _guard.Begin(_source, P("out"), true);
        Fill(session.StagingFolder, "new.txt", "new");

        session.Commit();

        Assert.NotNull(session.BackupFolder);
        Assert.StartsWith(P("out.backup-"), session.BackupFolder);
        Assert.Equal(oldSnapshot, Snapshot(session.BackupFolder!));
        Assert.Equal("new", File.ReadAllText(P("out/new.txt")));
        Assert.False(File.Exists(P("out/old.txt")));
    }

    [Fact]
    public void A_backup_name_clash_gets_a_numeric_suffix()
    {
        Fill(P("out"), "old.txt", "old");
        using var session = _guard.Begin(_source, P("out"), true);
        var taken = new List<string>();
        for (var i = -2; i <= 2; i++)
        {
            var name = P("out.backup-" + DateTime.Now.AddSeconds(i).ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(name);
            taken.Add(name);
        }

        session.Commit();

        Assert.NotNull(session.BackupFolder);
        Assert.DoesNotContain(session.BackupFolder, taken);
        Assert.Matches(@"out\.backup-\d{14}-\d+$", session.BackupFolder);
        Assert.Equal("old", File.ReadAllText(Path.Combine(session.BackupFolder!, "old.txt")));
    }

    [Fact]
    public void A_file_appearing_at_the_output_path_fails_the_commit_and_loses_nothing()
    {
        using var session = _guard.Begin(_source, P("out"), false);
        Fill(session.StagingFolder, "new.txt", "new");
        File.WriteAllText(P("out"), "squatter");

        Assert.Throws<IOException>(session.Commit);

        Assert.False(session.Committed);
        Assert.Equal("squatter", File.ReadAllText(P("out")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(session.StagingFolder, "new.txt")));
    }

    [Fact]
    public void A_failure_after_the_backup_move_restores_the_old_output()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // The open-handle trick below blocks a directory rename on Windows only.
        }

        Fill(P("out"), "old.txt", "old");
        var oldSnapshot = Snapshot(P("out"));
        using var session = _guard.Begin(_source, P("out"), true);
        Fill(session.StagingFolder, "new.txt", "new");
        using var handle = new FileStream(Path.Combine(session.StagingFolder, "new.txt"), FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.ThrowsAny<Exception>(session.Commit);

        Assert.False(session.Committed);
        Assert.Null(session.BackupFolder);
        Assert.Equal(oldSnapshot, Snapshot(P("out")));
        Assert.Empty(Directory.GetDirectories(_root, "out.backup-*"));
        Assert.True(File.Exists(Path.Combine(session.StagingFolder, "INCOMPLETE.txt")));
    }

    [Fact]
    public void Dispose_without_commit_leaves_staging_with_its_marker_and_the_output_alone()
    {
        Fill(P("out"), "old.txt", "old");
        var oldSnapshot = Snapshot(P("out"));
        var session = _guard.Begin(_source, P("out"), true);
        Fill(session.StagingFolder, "partial.txt", "partial");

        session.Dispose();

        Assert.True(File.Exists(Path.Combine(session.StagingFolder, "INCOMPLETE.txt")));
        Assert.Equal("partial", File.ReadAllText(Path.Combine(session.StagingFolder, "partial.txt")));
        Assert.Equal(oldSnapshot, Snapshot(P("out")));
        Assert.False(session.Committed);
    }

    [Fact]
    public void Commit_twice_or_after_dispose_throws()
    {
        var committed = _guard.Begin(_source, P("a"), false);
        committed.Commit();
        Assert.Throws<InvalidOperationException>(committed.Commit);
        Assert.True(Directory.Exists(P("a")));

        var disposed = _guard.Begin(_source, P("b"), false);
        disposed.Dispose();
        Assert.Throws<InvalidOperationException>(disposed.Commit);
        Assert.False(Directory.Exists(P("b")));
        Assert.True(Directory.Exists(disposed.StagingFolder));
    }

    [Fact]
    public void The_source_is_identical_after_check_begin_commit_and_a_failed_run()
    {
        var before = Snapshot(_source);

        _guard.Check(_source, P("out"), false);
        Assert.Equal(before, Snapshot(_source));

        using (var session = _guard.Begin(_source, P("out"), false))
        {
            Fill(session.StagingFolder, "x.txt", "x");
            Assert.Equal(before, Snapshot(_source));
            session.Commit();
        }

        Assert.Equal(before, Snapshot(_source));

        Assert.Throws<OutputGuardException>(() => _guard.Begin(_source, _source, true));
        Assert.Throws<OutputGuardException>(() => _guard.Begin(_source, Path.Combine(_source, "sub"), true));
        Assert.Throws<OutputGuardException>(() => _guard.Begin(_source, P("out"), false));
        Assert.Equal(before, Snapshot(_source));

        using (var failed = _guard.Begin(_source, P("out"), true))
        {
            File.Delete(Path.Combine(failed.StagingFolder, "INCOMPLETE.txt"));
        }

        Assert.Equal(before, Snapshot(_source));
    }
}
