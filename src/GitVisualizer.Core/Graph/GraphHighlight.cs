using System.Collections.Generic;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Graph;

/// <summary>
/// コミットグラフで強調表示する線と点。
/// 選択したコミットの線(同じ色の線)の先端から、第 1 親を辿った経路全体を強調する。
/// </summary>
public sealed class GraphHighlight
{
    private readonly HashSet<string> _shas;
    private readonly HashSet<GraphEdge> _edges;

    private GraphHighlight(HashSet<string> shas, HashSet<GraphEdge> edges)
    {
        _shas = shas;
        _edges = edges;
    }

    /// <summary>コミットの点を強調するかどうか。</summary>
    /// <param name="node">行データ。</param>
    /// <returns>経路上のコミットなら <see langword="true"/>。</returns>
    public bool Contains(GraphNode node) => _shas.Contains(node.Sha);

    /// <summary>線を強調するかどうか。</summary>
    /// <param name="edge">線。</param>
    /// <returns>経路上の線なら <see langword="true"/>。</returns>
    public bool Contains(GraphEdge edge) => _edges.Contains(edge);

    /// <summary>選択したコミットを通る経路を求めます。</summary>
    /// <param name="commits">コミット一覧(<see cref="CommitGraphBuilder.Build"/> に渡したもの)。</param>
    /// <param name="nodes"><see cref="CommitGraphBuilder.Build"/> の結果。</param>
    /// <param name="selectedIndex">選択したコミットの行番号。</param>
    /// <returns>強調する線と点。</returns>
    public static GraphHighlight Trace(IReadOnlyList<CommitInfo> commits, IReadOnlyList<GraphNode> nodes, int selectedIndex)
    {
        var shas = new HashSet<string>();
        var edges = new HashSet<GraphEdge>();

        // 同じ色の線は、ブランチの先端から第 1 親を辿った一続きの経路。その先端(最も上の行)から辿る
        int colorIndex = nodes[selectedIndex].ColorIndex;
        int row = selectedIndex;
        for (int i = 0; i < selectedIndex; i++)
        {
            if (nodes[i].ColorIndex == colorIndex)
            {
                row = i;
                break;
            }
        }

        while (row >= 0)
        {
            GraphNode node = nodes[row];
            shas.Add(node.Sha);

            // 親への線は親の順に並んでいるため、最初の Outgoing が第 1 親への線
            GraphEdge? toParent = null;
            if (commits[row].ParentShas.Count > 0)
            {
                foreach (GraphEdge edge in node.Edges)
                {
                    if (edge.Kind == GraphEdgeKind.Outgoing)
                    {
                        toParent = edge;
                        break;
                    }
                }
            }

            if (toParent == null)
            {
                break;
            }

            edges.Add(toParent);
            row = FollowLane(nodes, row + 1, toParent.ToLane, edges);
        }

        return new GraphHighlight(shas, edges);
    }

    /// <summary>レーンを下に辿り、親のコミットの行番号を返します。親が読み込まれていなければ -1。</summary>
    private static int FollowLane(IReadOnlyList<GraphNode> nodes, int startRow, int lane, HashSet<GraphEdge> edges)
    {
        for (int row = startRow; row < nodes.Count; row++)
        {
            GraphEdge? next = null;
            foreach (GraphEdge edge in nodes[row].Edges)
            {
                if (edge.FromLane == lane && edge.Kind != GraphEdgeKind.Outgoing)
                {
                    next = edge;
                    break;
                }
            }

            if (next == null)
            {
                return -1;
            }

            edges.Add(next);
            if (next.Kind == GraphEdgeKind.Incoming)
            {
                return row;
            }

            lane = next.ToLane;
        }

        return -1;
    }
}
