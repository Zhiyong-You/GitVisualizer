using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="IBranchService"/> の git.exe 実装。</summary>
public sealed class BranchService : IBranchService
{
    private readonly IGitCommandRunner _runner;
    private readonly GitOutputParser _parser;

    /// <summary><see cref="BranchService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    /// <param name="parser">出力パーサー。</param>
    public BranchService(IGitCommandRunner runner, GitOutputParser parser)
    {
        _runner = runner;
        _parser = parser;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(string repositoryPath, CancellationToken ct = default)
    {
        // TODO: git for-each-ref refs/heads refs/remotes --format=... を実行し GitOutputParser.ParseBranches に渡す
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> CheckoutAsync(string repositoryPath, BranchInfo branch, CheckoutOptions options, CancellationToken ct = default)
    {
        // TODO: git switch <name>(リモートなら --track)を実行。未コミット変更の扱いは options.Handling に従う(Stash: git stash push/pop、Discard: --discard-changes)
        throw new NotImplementedException();
    }
}
