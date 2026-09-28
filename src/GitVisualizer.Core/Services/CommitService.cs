using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="ICommitService"/> の git.exe 実装。</summary>
public sealed class CommitService : ICommitService
{
    private readonly IGitCommandRunner _runner;
    private readonly GitOutputParser _parser;

    /// <summary><see cref="CommitService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    /// <param name="parser">出力パーサー。</param>
    public CommitService(IGitCommandRunner runner, GitOutputParser parser)
    {
        _runner = runner;
        _parser = parser;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<FileChange>> GetStatusAsync(string repositoryPath, CancellationToken ct = default)
    {
        // TODO: git status --porcelain=v2 -z --untracked-files=all を実行し GitOutputParser.ParseStatus に渡す
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> StageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        // TODO: git add -- <paths...> を実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> UnstageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        // TODO: git restore --staged -- <paths...> を実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> CommitAsync(string repositoryPath, string message, bool amend, CancellationToken ct = default)
    {
        // TODO: メッセージを一時ファイル(UTF-8)に書き、git commit [--amend] -F <tmp> を実行
        throw new NotImplementedException();
    }
}
