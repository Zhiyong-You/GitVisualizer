using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="IRepositoryStateService"/> の git.exe 実装。</summary>
public sealed class RepositoryStateService : IRepositoryStateService, IDisposable
{
    private readonly IGitCommandRunner _runner;
    private readonly object _lock = new();
    private RepositoryWatcher? _watcher;
    private string? _watchedPath;

    /// <summary><see cref="RepositoryStateService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    public RepositoryStateService(IGitCommandRunner runner)
    {
        _runner = runner;
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public async Task<RepositoryOperation> GetOperationAsync(string repositoryPath, CancellationToken ct = default)
    {
        GitResult result = await _runner.RunAsync(repositoryPath, new[] { "rev-parse", "--git-dir" }, ct: ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError);
        }

        // --git-dir は相対パス(".git")で返ることがある
        string gitDir = Path.GetFullPath(Path.Combine(repositoryPath, result.StandardOutput.Trim()));

        if (Directory.Exists(Path.Combine(gitDir, "rebase-merge")) || Directory.Exists(Path.Combine(gitDir, "rebase-apply")))
        {
            return RepositoryOperation.Rebase;
        }

        if (File.Exists(Path.Combine(gitDir, "CHERRY_PICK_HEAD")))
        {
            return RepositoryOperation.CherryPick;
        }

        if (File.Exists(Path.Combine(gitDir, "REVERT_HEAD")))
        {
            return RepositoryOperation.Revert;
        }

        if (File.Exists(Path.Combine(gitDir, "MERGE_HEAD")))
        {
            return RepositoryOperation.Merge;
        }

        return RepositoryOperation.None;
    }

    /// <inheritdoc/>
    public void Watch(string? repositoryPath)
    {
        lock (_lock)
        {
            if (string.Equals(_watchedPath, repositoryPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            StopWatching();
            _watchedPath = repositoryPath;
            if (repositoryPath == null)
            {
                return;
            }

            // RepositoryWatcher.Changed を中継して発火させる
            var watcher = new RepositoryWatcher(repositoryPath);
            watcher.Changed += OnWatcherChanged;
            watcher.Start();
            _watcher = watcher;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            StopWatching();
            _watchedPath = null;
        }
    }

    private void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.Changed -= OnWatcherChanged;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnWatcherChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
