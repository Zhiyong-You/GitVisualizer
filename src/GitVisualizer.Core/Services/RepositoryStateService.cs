using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="IRepositoryStateService"/> の git.exe 実装。</summary>
public sealed class RepositoryStateService : IRepositoryStateService
{
    private readonly IGitCommandRunner _runner;

    /// <summary><see cref="RepositoryStateService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    public RepositoryStateService(IGitCommandRunner runner)
    {
        _runner = runner;
    }

    // TODO: 実装時に削除(未使用イベントの警告 CS0067 を抑止している)。RepositoryWatcher.Changed を中継して発火させる
#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler? Changed;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public Task<RepositoryOperation> GetOperationAsync(string repositoryPath, CancellationToken ct = default)
    {
        // TODO: git rev-parse --git-dir で .git を求め、rebase-merge / rebase-apply / CHERRY_PICK_HEAD / REVERT_HEAD / MERGE_HEAD の有無で判定
        throw new NotImplementedException();
    }
}
