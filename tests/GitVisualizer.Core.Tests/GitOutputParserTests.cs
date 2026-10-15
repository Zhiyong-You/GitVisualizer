using System.Linq;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class GitOutputParserTests
{
    private readonly GitOutputParser _parser = new();

    [Fact]
    public void ParseBranches_ローカルとリモートを解析しリモートのHEADは除外する()
    {
        string output =
            "*\0refs/heads/main\0main\0aaaa\0origin/main\0[ahead 1, behind 2]\n" +
            " \0refs/heads/feature\0feature\0bbbb\0\0\n" +
            " \0refs/remotes/origin/HEAD\0origin\0aaaa\0\0\n" +
            " \0refs/remotes/origin/main\0origin/main\0cccc\0\0\n";

        var branches = _parser.ParseBranches(output);

        Assert.Equal(new[] { "main", "feature", "origin/main" }, branches.Select(b => b.Name));
        Assert.True(branches[0].IsCurrent);
        Assert.Equal("origin/main", branches[0].Upstream);
        Assert.Equal(1, branches[0].Ahead);
        Assert.Equal(2, branches[0].Behind);
        Assert.Null(branches[1].Upstream);
        Assert.True(branches[2].IsRemote);
    }

    [Fact]
    public void ParseLog_実際のgit_log_z出力を解析する()
    {
        // git log -z --format=%H%x00%P%x00%an%x00%ae%x00%aI%x00%D%x00%s%x00%b の実出力(本文の末尾に改行が付く)
        string output =
            "2222222222222222222222222222222222222222\0" + "1111111111111111111111111111111111111111\0a\0a@b\0" +
            "2026-10-13T10:37:15+09:00\0HEAD -> main, tag: v1.0, origin/main\0second\0body line\n\0" +
            "1111111111111111111111111111111111111111\0\0a\0a@b\02026-10-13T10:37:14+09:00\0\0first\0\0";

        var commits = _parser.ParseLog(output);

        Assert.Equal(2, commits.Count);
        Assert.Equal("2222222222222222222222222222222222222222", commits[0].Sha);
        Assert.Equal(new[] { "1111111111111111111111111111111111111111" }, commits[0].ParentShas);
        Assert.Equal("second", commits[0].Subject);
        Assert.Equal("body line", commits[0].Body);
        Assert.Equal(new[] { "HEAD -> main", "v1.0", "origin/main" }, commits[0].Refs);
        Assert.Equal(9, commits[0].AuthorDate.Offset.Hours);
        Assert.Empty(commits[1].ParentShas);
        Assert.Empty(commits[1].Refs);
        Assert.Equal(string.Empty, commits[1].Body);
    }

    [Fact]
    public void ParseStatus_各種レコードを解析する()
    {
        string output =
            "1 M. N... 100644 100644 100644 aaaa bbbb staged.txt\0" +
            "1 .M N... 100644 100644 100644 aaaa bbbb dir/work tree.txt\0" +
            "2 RM N... 100644 100644 100644 aaaa bbbb R100 new.txt\0old.txt\0" +
            "u UU N... 100644 100644 100644 100644 aaaa bbbb cccc conflict.txt\0" +
            "? untracked.txt\0";

        var changes = _parser.ParseStatus(output);

        Assert.Collection(
            changes,
            c => { Assert.Equal("staged.txt", c.Path); Assert.Equal(FileChangeKind.Modified, c.Kind); Assert.True(c.IsStaged); },
            c => { Assert.Equal("dir/work tree.txt", c.Path); Assert.False(c.IsStaged); },
            c => { Assert.Equal("new.txt", c.Path); Assert.Equal("old.txt", c.OldPath); Assert.Equal(FileChangeKind.Renamed, c.Kind); Assert.True(c.IsStaged); },
            c => { Assert.Equal("new.txt", c.Path); Assert.Equal(FileChangeKind.Modified, c.Kind); Assert.False(c.IsStaged); },
            c => { Assert.Equal("conflict.txt", c.Path); Assert.Equal(FileChangeKind.Conflicted, c.Kind); },
            c => { Assert.Equal("untracked.txt", c.Path); Assert.Equal(FileChangeKind.Untracked, c.Kind); });
    }

    [Fact]
    public void ParseNameStatus_名前変更を含む出力を解析する()
    {
        var changes = _parser.ParseNameStatus("M\0a.txt\0R095\0old.txt\0new.txt\0D\0gone.txt\0A\0added.txt\0");

        Assert.Collection(
            changes,
            c => { Assert.Equal("a.txt", c.Path); Assert.Equal(FileChangeKind.Modified, c.Kind); },
            c => { Assert.Equal("new.txt", c.Path); Assert.Equal("old.txt", c.OldPath); Assert.Equal(FileChangeKind.Renamed, c.Kind); },
            c => { Assert.Equal("gone.txt", c.Path); Assert.Equal(FileChangeKind.Deleted, c.Kind); },
            c => { Assert.Equal("added.txt", c.Path); Assert.Equal(FileChangeKind.Added, c.Kind); });
    }

    [Theory]
    [InlineData("Receiving objects:  45% (9/20), 1.20 MiB | 500.00 KiB/s", 45)]
    [InlineData("remote: Counting objects:  10% (2/20)\rremote: Counting objects: 100% (20/20), done.", 100)]
    public void TryParseProgress_進捗行を解析する(string line, int expected)
    {
        Assert.True(_parser.TryParseProgress(line, out var progress));
        Assert.Equal(expected, progress!.Percent);
    }

    [Theory]
    [InlineData("fatal: 50% of something failed")]
    [InlineData("")]
    public void TryParseProgress_進捗行でなければfalse(string line)
    {
        Assert.False(_parser.TryParseProgress(line, out var progress));
        Assert.Null(progress);
    }
}
