using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="ISyncService"/> の git.exe 実装。</summary>
public sealed class SyncService : ISyncService
{
    private readonly IGitCommandRunner _runner;

    /// <summary><see cref="SyncService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    public SyncService(IGitCommandRunner runner)
    {
        _runner = runner;
    }

    /// <inheritdoc/>
    public Task<GitResult> FetchAsync(string repositoryPath, string? remote, IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        // TODO: git fetch --progress --prune <remote>(remote が null なら --all)を実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> PullAsync(string repositoryPath, PullOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        // TODO: git pull --progress に options.Mode に応じて --no-rebase / --rebase / --ff-only、AutoStash なら --autostash を付けて実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> PushAsync(string repositoryPath, PushOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        // TODO: git push --progress [-u] [--force-with-lease] [--follow-tags] <remote> <branch> を実行
        throw new NotImplementedException();
    }
}
