using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary>コミット履歴の取得と書き換え(rebase / cherry-pick / reword)。</summary>
public interface IHistoryService
{
    /// <summary>コミット履歴を取得します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="maxCount">最大取得件数。</param>
    /// <param name="revisions">
    /// 履歴をたどる起点(例: <c>HEAD</c>, <c>refs/remotes/origin/main</c>)。チェックアウトせずにそのブランチの履歴を取得できる。
    /// 複数指定するといずれかから辿れるコミットをまとめて返す。<see langword="null"/> なら全ブランチ・リモート・タグ・HEAD。
    /// </param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>コミット一覧(トポロジカル順、新しい順)。存在しない起点は無視し、1 つも存在しなければ空。</returns>
    Task<IReadOnlyList<CommitInfo>> GetLogAsync(string repositoryPath, int maxCount, IReadOnlyList<string>? revisions = null, CancellationToken ct = default);

    /// <summary>コミットで変更されたファイルを取得します(マージコミットは第 1 親との差分)。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="commitSha">対象のコミット SHA。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>変更ファイル一覧。</returns>
    Task<IReadOnlyList<FileChange>> GetCommitChangesAsync(string repositoryPath, string commitSha, CancellationToken ct = default);

    /// <summary>
    /// コミットで変更されたファイルの変更前(第 1 親)と変更後の内容を一時ファイルに書き出します。
    /// 追加・削除などで存在しない側は空ファイルになります。
    /// </summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="commit">対象のコミット。</param>
    /// <param name="file">対象のファイル(<see cref="GetCommitChangesAsync"/> の結果の 1 件)。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>差分表示用の一時ファイルと見出し。</returns>
    Task<FileDiff> GetCommitFileDiffAsync(string repositoryPath, CommitInfo commit, FileChange file, CancellationToken ct = default);

    /// <summary>現在のブランチをリベースします。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="options">rebase 設定。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。競合で停止した場合も失敗として返ります。</returns>
    Task<GitResult> RebaseAsync(string repositoryPath, RebaseOptions options, CancellationToken ct = default);

    /// <summary>指定コミットを現在のブランチに取り込みます。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="commitShas">取り込むコミット SHA(古い順)。</param>
    /// <param name="options">cherry-pick 設定。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> CherryPickAsync(string repositoryPath, IReadOnlyList<string> commitShas, CherryPickOptions options, CancellationToken ct = default);

    /// <summary>指定コミットのメッセージを変更します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="commitSha">変更対象のコミット SHA。</param>
    /// <param name="newMessage">新しいコミットメッセージ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> RewordAsync(string repositoryPath, string commitSha, string newMessage, CancellationToken ct = default);

    /// <summary>中断中の操作(rebase / cherry-pick)を続行します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> ContinueAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>中断中の操作で、現在のコミットをスキップします。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> SkipAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>中断中の操作を取り消し、開始前の状態に戻します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> AbortAsync(string repositoryPath, CancellationToken ct = default);
}
