using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary>作業ツリーの状態取得・ステージ・コミット。</summary>
public interface ICommitService
{
    /// <summary>作業ツリーとインデックスの変更一覧を取得します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>変更ファイル一覧。</returns>
    Task<IReadOnlyList<FileChange>> GetStatusAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>ファイルをインデックスに追加します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="paths">対象ファイルの相対パス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> StageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>ファイルをインデックスから外します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="paths">対象ファイルの相対パス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> UnstageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default);

    /// <summary>変更ファイルの diff を取得します(ステージ済みならインデックスと HEAD、未ステージなら作業ツリーとインデックスの差分)。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="change">対象の変更ファイル。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>1 ファイル分の diff。</returns>
    Task<FilePatch> GetFilePatchAsync(string repositoryPath, FileChange change, CancellationToken ct = default);

    /// <summary>未ステージの diff のうち、選択した行だけをインデックスに追加します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="patch"><see cref="GetFilePatchAsync"/> で取得した未ステージの diff。</param>
    /// <param name="lines">ステージする追加行・削除行。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> StageLinesAsync(string repositoryPath, FilePatch patch, IReadOnlyCollection<DiffLine> lines, CancellationToken ct = default);

    /// <summary>ステージ済みの diff のうち、選択した行だけをインデックスから外します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="patch"><see cref="GetFilePatchAsync"/> で取得したステージ済みの diff。</param>
    /// <param name="lines">ステージを解除する追加行・削除行。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> UnstageLinesAsync(string repositoryPath, FilePatch patch, IReadOnlyCollection<DiffLine> lines, CancellationToken ct = default);

    /// <summary>ステージ済みの変更をコミットします。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="message">コミットメッセージ。</param>
    /// <param name="amend">直前のコミットを修正するかどうか。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> CommitAsync(string repositoryPath, string message, bool amend, CancellationToken ct = default);
}
