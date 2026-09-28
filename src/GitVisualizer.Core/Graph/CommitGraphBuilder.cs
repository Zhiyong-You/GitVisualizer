using System;
using System.Collections.Generic;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Graph;

/// <summary>コミット一覧からコミットグラフ(レーン配置)を計算します。</summary>
public sealed class CommitGraphBuilder
{
    /// <summary>コミットグラフを計算します。</summary>
    /// <param name="commits">コミット一覧。<c>git log --topo-order</c> の順(新しい順)であること。</param>
    /// <returns><paramref name="commits"/> と同じ順序・同じ件数の行データ。</returns>
    public IReadOnlyList<GraphNode> Build(IReadOnlyList<CommitInfo> commits)
    {
        // TODO: git log --topo-order の順に ParentShas をたどり、各行のレーンと線を割り当てる(git 実行はしない)
        throw new NotImplementedException();
    }
}
