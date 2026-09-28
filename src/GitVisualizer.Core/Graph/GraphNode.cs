using System.Collections.Generic;

namespace GitVisualizer.Core.Graph;

/// <summary>コミットグラフの 1 行(= 1 コミット)分の描画情報。</summary>
public sealed class GraphNode
{
    /// <summary><see cref="GraphNode"/> を初期化します。</summary>
    /// <param name="sha">この行のコミット SHA。</param>
    /// <param name="lane">コミットの点を描くレーン番号(0 始まり)。</param>
    /// <param name="colorIndex">点の描画色のインデックス。</param>
    /// <param name="edges">この行を通過する線の一覧。</param>
    public GraphNode(string sha, int lane, int colorIndex, IReadOnlyList<GraphEdge> edges)
    {
        Sha = sha;
        Lane = lane;
        ColorIndex = colorIndex;
        Edges = edges;
    }

    /// <summary>この行のコミット SHA。</summary>
    public string Sha { get; }

    /// <summary>コミットの点を描くレーン番号(0 始まり)。</summary>
    public int Lane { get; }

    /// <summary>点の描画色のインデックス。</summary>
    public int ColorIndex { get; }

    /// <summary>この行を通過する線の一覧。</summary>
    public IReadOnlyList<GraphEdge> Edges { get; }
}
