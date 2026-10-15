using System;
using System.Linq;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Models;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class CommitGraphBuilderTests
{
    private readonly CommitGraphBuilder _builder = new();

    private static CommitInfo Commit(string sha, params string[] parents) =>
        new(sha, parents, "a", "a@b", DateTimeOffset.Now, sha, string.Empty, Array.Empty<string>());

    [Fact]
    public void Build_直線の履歴は全てレーン0()
    {
        var nodes = _builder.Build(new[] { Commit("c", "b"), Commit("b", "a"), Commit("a") });

        Assert.All(nodes, n => Assert.Equal(0, n.Lane));

        // 先端は上から線が入らず、ルートは下へ線が出ない
        Assert.DoesNotContain(nodes[0].Edges, e => e.Kind == GraphEdgeKind.Incoming);
        Assert.Contains(nodes[0].Edges, e => e.Kind == GraphEdgeKind.Outgoing && e.ToLane == 0);
        Assert.Contains(nodes[2].Edges, e => e.Kind == GraphEdgeKind.Incoming && e.FromLane == 0);
        Assert.DoesNotContain(nodes[2].Edges, e => e.Kind == GraphEdgeKind.Outgoing);
    }

    [Fact]
    public void Build_分岐とマージを別レーンに配置する()
    {
        // m = merge(b, f), f の親 a, b の親 a
        var nodes = _builder.Build(new[]
        {
            Commit("m", "b", "f"),
            Commit("f", "a"),
            Commit("b", "a"),
            Commit("a"),
        });

        Assert.Equal(new[] { 0, 1, 0, 0 }, nodes.Select(n => n.Lane));

        // マージコミットから 2 本の線が出る
        Assert.Equal(new[] { 0, 1 }, nodes[0].Edges.Where(e => e.Kind == GraphEdgeKind.Outgoing).Select(e => e.ToLane).OrderBy(x => x));

        // f の行ではレーン 0 が素通りする
        Assert.Contains(nodes[1].Edges, e => e.Kind == GraphEdgeKind.PassThrough && e.FromLane == 0 && e.ToLane == 0);

        // b の行で、a を待つ f からの線(レーン 1)がレーン 0 に寄せられ、a はレーン 0 で受ける
        Assert.Contains(nodes[2].Edges, e => e.Kind == GraphEdgeKind.PassThrough && e.FromLane == 1 && e.ToLane == 0);
        Assert.Contains(nodes[3].Edges, e => e.Kind == GraphEdgeKind.Incoming && e.FromLane == 0 && e.ToLane == 0);
        Assert.NotEqual(nodes[0].ColorIndex, nodes[1].ColorIndex);
    }

    [Fact]
    public void Build_入力と同じ件数と順序を返す()
    {
        var commits = new[] { Commit("x", "y"), Commit("z"), Commit("y") };

        var nodes = _builder.Build(commits);

        Assert.Equal(commits.Select(c => c.Sha), nodes.Select(n => n.Sha));
    }
}
