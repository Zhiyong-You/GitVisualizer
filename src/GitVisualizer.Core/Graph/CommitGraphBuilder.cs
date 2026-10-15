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
        // lanes[i]: レーン i で次に現れるはずのコミット SHA(空きレーンは null)
        var lanes = new List<string?>();
        var laneColors = new List<int>();
        int nextColor = 0;
        var nodes = new List<GraphNode>(commits.Count);

        foreach (CommitInfo commit in commits)
        {
            // 行の上端の状態を保存しておく
            string?[] top = lanes.ToArray();
            int[] topColors = laneColors.ToArray();
            var edges = new List<GraphEdge>();

            // 1. この行の点のレーンを決める。子から参照されていなければ(ブランチの先端)空きレーンを使う
            int lane = lanes.IndexOf(commit.Sha);
            if (lane < 0)
            {
                lane = AllocateLane(lanes, laneColors);
                laneColors[lane] = nextColor++;
            }

            int color = laneColors[lane];

            // 2. このコミットを待っていたレーン(子からの線)はすべてこの点に合流して終わる
            for (int i = 0; i < top.Length; i++)
            {
                if (top[i] == commit.Sha)
                {
                    edges.Add(new GraphEdge(i, lane, topColors[i], GraphEdgeKind.Incoming));
                    lanes[i] = null;
                }
            }

            // 3. 親へ線を伸ばす。第 1 親は同じレーンを引き継ぎ、既に別レーンで待たれていればそこへ合流する
            int movedFrom = -1;
            for (int p = 0; p < commit.ParentShas.Count; p++)
            {
                string parent = commit.ParentShas[p];
                int target = lanes.IndexOf(parent);
                if (target > lane && p == 0 && lanes[lane] == null)
                {
                    // 第 1 親が右のレーンで待たれている場合は、左(このレーン)に寄せてグラフを細く保つ
                    lanes[lane] = parent;
                    laneColors[lane] = color;
                    lanes[target] = null;
                    movedFrom = target;
                    target = lane;
                }
                else if (target < 0)
                {
                    if (p == 0 && lanes[lane] == null)
                    {
                        target = lane;
                        laneColors[target] = color;
                    }
                    else
                    {
                        target = AllocateLane(lanes, laneColors);
                        laneColors[target] = nextColor++;
                    }

                    lanes[target] = parent;
                }

                // 第 1 親への線は点の色、マージ元(第 2 親以降)への線はそのレーンの色で描く
                edges.Add(new GraphEdge(lane, target, p == 0 ? color : laneColors[target], GraphEdgeKind.Outgoing));
            }

            // 4. この行と関係のないレーンは素通りする(左に寄せたレーンは斜めに移る)
            for (int i = 0; i < top.Length; i++)
            {
                if (top[i] != null && top[i] != commit.Sha)
                {
                    edges.Add(new GraphEdge(i, i == movedFrom ? lane : i, topColors[i], GraphEdgeKind.PassThrough));
                }
            }

            // 右端の空きレーンは詰める
            while (lanes.Count > 0 && lanes[lanes.Count - 1] == null)
            {
                lanes.RemoveAt(lanes.Count - 1);
                laneColors.RemoveAt(laneColors.Count - 1);
            }

            nodes.Add(new GraphNode(commit.Sha, lane, color, edges));
        }

        return nodes;
    }

    /// <summary>空きレーン(なければ右端に追加)の番号を返します。</summary>
    private static int AllocateLane(List<string?> lanes, List<int> laneColors)
    {
        int free = lanes.IndexOf(null);
        if (free >= 0)
        {
            return free;
        }

        lanes.Add(null);
        laneColors.Add(0);
        return lanes.Count - 1;
    }
}
