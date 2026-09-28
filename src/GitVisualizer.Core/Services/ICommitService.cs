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

    /// <summary>ステージ済みの変更をコミットします。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="message">コミットメッセージ。</param>
    /// <param name="amend">直前のコミットを修正するかどうか。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> CommitAsync(string repositoryPath, string message, bool amend, CancellationToken ct = default);
}
