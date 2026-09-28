using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary>リモートとの同期(fetch / pull / push)。</summary>
public interface ISyncService
{
    /// <summary>リモートから取得します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="remote">リモート名。<see langword="null"/> なら全リモート。</param>
    /// <param name="progress">進捗の通知先。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> FetchAsync(string repositoryPath, string? remote, IProgress<OperationProgress>? progress = null, CancellationToken ct = default);

    /// <summary>リモートから取得して現在のブランチに統合します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="options">pull 設定。</param>
    /// <param name="progress">進捗の通知先。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> PullAsync(string repositoryPath, PullOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default);

    /// <summary>リモートへ送信します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="options">push 設定。</param>
    /// <param name="progress">進捗の通知先。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> PushAsync(string repositoryPath, PushOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default);
}
