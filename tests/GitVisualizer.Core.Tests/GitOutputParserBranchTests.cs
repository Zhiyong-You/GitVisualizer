using System.Linq;
using GitVisualizer.Core.Infrastructure;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class GitOutputParserBranchTests
{
    private const string Sha1 = "a41081e198c0cca8f3f3d747448fc4d4f62533bc";
    private const string Sha2 = "89be6748ae36d62b4ea87c95248da4b3bf204da6";

    private readonly GitOutputParser _parser = new();

    private static string Line(params string[] fields) => string.Join("\0", fields) + "\n";

    [Fact]
    public void ParseBranches_ローカルとリモートを解析できる()
    {
        string output =
            Line("*", "refs/heads/main", "main", Sha1, "origin/main", "[ahead 1, behind 1]") +
            Line(" ", "refs/remotes/origin/main", "origin/main", Sha2, "", "");

        var branches = _parser.ParseBranches(output);

        Assert.Equal(2, branches.Count);

        var local = branches[0];
        Assert.Equal("main", local.Name);
        Assert.Equal("refs/heads/main", local.FullName);
        Assert.Equal(Sha1, local.TipSha);
        Assert.False(local.IsRemote);
        Assert.True(local.IsCurrent);
        Assert.Equal("origin/main", local.Upstream);
        Assert.Equal(1, local.Ahead);
        Assert.Equal(1, local.Behind);

        var remote = branches[1];
        Assert.True(remote.IsRemote);
        Assert.False(remote.IsCurrent);
        Assert.Null(remote.Upstream);
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("[ahead 3]", 3, 0)]
    [InlineData("[behind 2]", 0, 2)]
    [InlineData("[ahead 12, behind 34]", 12, 34)]
    [InlineData("[gone]", 0, 0)]
    public void ParseBranches_上流との差分を解析できる(string track, int ahead, int behind)
    {
        var branch = _parser.ParseBranches(Line(" ", "refs/heads/dev", "dev", Sha1, "origin/dev", track)).Single();

        Assert.Equal(ahead, branch.Ahead);
        Assert.Equal(behind, branch.Behind);
    }

    [Fact]
    public void ParseBranches_リモートのHEADは除外する()
    {
        string output =
            Line(" ", "refs/remotes/origin/HEAD", "origin", Sha2, "", "") +
            Line(" ", "refs/remotes/origin/main", "origin/main", Sha2, "", "");

        var branch = Assert.Single(_parser.ParseBranches(output));
        Assert.Equal("origin/main", branch.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    public void ParseBranches_空出力なら空リスト(string output)
    {
        Assert.Empty(_parser.ParseBranches(output));
    }

    [Fact]
    public void ParseBranches_CRLFでも解析できる()
    {
        string output = Line(" ", "refs/heads/dev", "dev", Sha1, "", "").Replace("\n", "\r\n");

        Assert.Equal("dev", Assert.Single(_parser.ParseBranches(output)).Name);
    }
}
