namespace GitVisualizer.Core.Graph;

/// <summary>コミットグラフの 1 行内で、上端のレーンから下端のレーンへ引く線。</summary>
public sealed class GraphEdge
{
    /// <summary><see cref="GraphEdge"/> を初期化します。</summary>
    /// <param name="fromLane">行の上端側のレーン番号(0 始まり)。</param>
    /// <param name="toLane">行の下端側のレーン番号(0 始まり)。</param>
    /// <param name="colorIndex">描画色のインデックス。</param>
    /// <param name="kind">線の種類(行の中央にあるコミットの点との関係)。</param>
    public GraphEdge(int fromLane, int toLane, int colorIndex, GraphEdgeKind kind = GraphEdgeKind.PassThrough)
    {
        FromLane = fromLane;
        ToLane = toLane;
        ColorIndex = colorIndex;
        Kind = kind;
    }

    /// <summary>行の上端側のレーン番号(0 始まり)。</summary>
    public int FromLane { get; }

    /// <summary>行の下端側のレーン番号(0 始まり)。</summary>
    public int ToLane { get; }

    /// <summary>描画色のインデックス。</summary>
    public int ColorIndex { get; }

    /// <summary>線の種類。</summary>
    public GraphEdgeKind Kind { get; }
}
