using System;
using System.Collections.Generic;
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
        var arguments = new List<string> { "fetch", "--progress", "--prune" };
        arguments.Add(string.IsNullOrEmpty(remote) ? "--all" : remote!);

        return _runner.RunAsync(repositoryPath, arguments, progress: progress, ct: ct);
    }

    /// <inheritdoc/>
    public Task<GitResult> PullAsync(string repositoryPath, PullOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        var arguments = new List<string> { "pull", "--progress" };
        arguments.Add(options.Mode switch
        {
            PullMode.Rebase => "--rebase",
            PullMode.FastForwardOnly => "--ff-only",
            _ => "--no-rebase",
        });

        if (options.AutoStash)
        {
            arguments.Add("--autostash");
        }

        // リモート・ブランチを指定しなければ上流設定に従う(ブランチだけの指定はできない)
        if (!string.IsNullOrEmpty(options.Remote))
        {
            arguments.Add(options.Remote!);
            if (!string.IsNullOrEmpty(options.Branch))
            {
                arguments.Add(options.Branch!);
            }
        }

        return _runner.RunAsync(repositoryPath, arguments, progress: progress, ct: ct);
    }

    /// <inheritdoc/>
    public Task<GitResult> PushAsync(string repositoryPath, PushOptions options, IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        var arguments = new List<string> { "push", "--progress" };
        if (options.SetUpstream)
        {
            arguments.Add("-u");
        }

        if (options.ForceWithLease)
        {
            arguments.Add("--force-with-lease");
        }

        if (options.IncludeTags)
        {
            arguments.Add("--follow-tags");
        }

        arguments.Add(options.Remote);

        // ブランチ未指定なら現在のブランチ(HEAD は同名のリモートブランチに push される)
        arguments.Add(string.IsNullOrEmpty(options.Branch) ? "HEAD" : options.Branch!);

        return _runner.RunAsync(repositoryPath, arguments, progress: progress, ct: ct);
    }
}
