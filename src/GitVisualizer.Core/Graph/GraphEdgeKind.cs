namespace GitVisualizer.Core.Graph;

/// <summary>コミットグラフの線の種類。</summary>
public enum GraphEdgeKind
{
    /// <summary>行を素通りする線。上端の <see cref="GraphEdge.FromLane"/> から下端の <see cref="GraphEdge.ToLane"/> へ引く。</summary>
    PassThrough,

    /// <summary>上の行(子コミット)からこの行の点へ入る線。上端の <see cref="GraphEdge.FromLane"/> から点(<see cref="GraphEdge.ToLane"/>)へ引く。</summary>
    Incoming,

    /// <summary>この行の点から下の行(親コミット)へ出る線。点(<see cref="GraphEdge.FromLane"/>)から下端の <see cref="GraphEdge.ToLane"/> へ引く。</summary>
    Outgoing,
}
