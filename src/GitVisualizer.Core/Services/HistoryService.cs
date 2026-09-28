using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="IHistoryService"/> の git.exe 実装。</summary>
public sealed class HistoryService : IHistoryService
{
    private readonly IGitCommandRunner _runner;
    private readonly GitOutputParser _parser;
    private readonly IRepositoryStateService _state;
    private readonly string _editorHelperPath;

    /// <summary><see cref="HistoryService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    /// <param name="parser">出力パーサー。</param>
    /// <param name="state">中断中の操作を判定するためのサービス。</param>
    /// <param name="editorHelperPath">GitEditorHelper.exe のフルパス(reword で GIT_SEQUENCE_EDITOR / GIT_EDITOR に使う)。</param>
    public HistoryService(IGitCommandRunner runner, GitOutputParser parser, IRepositoryStateService state, string editorHelperPath)
    {
        _runner = runner;
        _parser = parser;
        _state = state;
        _editorHelperPath = editorHelperPath;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CommitInfo>> GetLogAsync(string repositoryPath, int maxCount, CancellationToken ct = default)
    {
        // TODO: git log --all --topo-order -n <maxCount> -z --format=... を実行し GitOutputParser.ParseLog に渡す
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> RebaseAsync(string repositoryPath, RebaseOptions options, CancellationToken ct = default)
    {
        // TODO: git rebase [--autostash] <options.Onto> を実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> CherryPickAsync(string repositoryPath, IReadOnlyList<string> commitShas, CherryPickOptions options, CancellationToken ct = default)
    {
        // TODO: git cherry-pick [--no-commit] [-x] [-m <n>] <sha...> を実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> RewordAsync(string repositoryPath, string commitSha, string newMessage, CancellationToken ct = default)
    {
        // TODO: HEAD なら git commit --amend -F <tmp>、それ以外は GIT_SEQUENCE_EDITOR/GIT_EDITOR=GitEditorHelper.exe で git rebase -i <sha>^
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> ContinueAsync(string repositoryPath, CancellationToken ct = default)
    {
        // TODO: IRepositoryStateService.GetOperationAsync の結果に応じて git rebase --continue / git cherry-pick --continue(GIT_EDITOR=true)
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> SkipAsync(string repositoryPath, CancellationToken ct = default)
    {
        // TODO: 操作に応じて git rebase --skip / git cherry-pick --skip を実行
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<GitResult> AbortAsync(string repositoryPath, CancellationToken ct = default)
    {
        // TODO: 操作に応じて git rebase --abort / git cherry-pick --abort / git merge --abort を実行
        throw new NotImplementedException();
    }
}
