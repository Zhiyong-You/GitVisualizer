using System;
using System.Collections.Generic;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>git コマンドの標準出力をモデルに変換します。</summary>
public sealed class GitOutputParser
{
    /// <summary>ブランチ一覧の出力を解析します。</summary>
    /// <param name="output"><c>git for-each-ref</c> の標準出力。</param>
    /// <returns>ブランチ一覧。</returns>
    public IReadOnlyList<BranchInfo> ParseBranches(string output)
    {
        // TODO: git for-each-ref --format="%(HEAD)%00%(refname)%00%(refname:short)%00%(objectname)%00%(upstream:short)%00%(upstream:track)" の出力を解析
        throw new NotImplementedException();
    }

    /// <summary>コミットログの出力を解析します。</summary>
    /// <param name="output"><c>git log</c> の標準出力。</param>
    /// <returns>コミット一覧(新しい順)。</returns>
    public IReadOnlyList<CommitInfo> ParseLog(string output)
    {
        // TODO: git log -z --format="%H%x00%P%x00%an%x00%ae%x00%aI%x00%D%x00%s%x00%b" の出力を解析
        throw new NotImplementedException();
    }

    /// <summary>作業ツリーの状態出力を解析します。</summary>
    /// <param name="output"><c>git status</c> の標準出力。</param>
    /// <returns>変更ファイル一覧(ステージ済み・未ステージの両方)。</returns>
    public IReadOnlyList<FileChange> ParseStatus(string output)
    {
        // TODO: git status --porcelain=v2 -z --untracked-files=all の出力を解析(1/2/u/? 行を区別)
        throw new NotImplementedException();
    }

    /// <summary>標準エラーの進捗行を解析します。</summary>
    /// <param name="line">標準エラーの 1 行(<c>\r</c> 区切りを含む)。</param>
    /// <param name="progress">解析結果。進捗行でなければ <see langword="null"/>。</param>
    /// <returns>進捗行として解析できた場合は <see langword="true"/>。</returns>
    public bool TryParseProgress(string line, out OperationProgress? progress)
    {
        // TODO: --progress 付きで出る "Receiving objects:  45% (9/20)" 形式の行から % を取り出す
        throw new NotImplementedException();
    }
}
