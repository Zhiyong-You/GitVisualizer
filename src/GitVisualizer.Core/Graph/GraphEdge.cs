namespace GitVisualizer.Core.Graph;

/// <summary>コミットグラフの 1 行内で、上端のレーンから下端のレーンへ引く線。</summary>
public sealed class GraphEdge
{
    /// <summary><see cref="GraphEdge"/> を初期化します。</summary>
    /// <param name="fromLane">行の上端側のレーン番号(0 始まり)。</param>
    /// <param name="toLane">行の下端側のレーン番号(0 始まり)。</param>
    /// <param name="colorIndex">描画色のインデックス。</param>
    public GraphEdge(int fromLane, int toLane, int colorIndex)
    {
        FromLane = fromLane;
        ToLane = toLane;
        ColorIndex = colorIndex;
    }

    /// <summary>行の上端側のレーン番号(0 始まり)。</summary>
    public int FromLane { get; }

    /// <summary>行の下端側のレーン番号(0 始まり)。</summary>
    public int ToLane { get; }

    /// <summary>描画色のインデックス。</summary>
    public int ColorIndex { get; }
}
