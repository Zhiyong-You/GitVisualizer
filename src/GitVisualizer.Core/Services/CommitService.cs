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
    public async Task<IReadOnlyList<FileChange>> GetStatusAsync(string repositoryPath, CancellationToken ct = default)
    {
        // --no-optional-locks: status がインデックスを書き換えないようにする(書き換えると RepositoryWatcher が反応し、更新が繰り返される)
        GitResult result = await _runner.RunAsync(
            repositoryPath,
            new[] { "--no-optional-locks", "status", "--porcelain=v2", "-z", "--untracked-files=all" },
            ct: ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError);
        }

        return _parser.ParseStatus(result.StandardOutput);
    }

    /// <inheritdoc/>
    public Task<GitResult> StageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return Task.FromResult(new GitResult(0, string.Empty, string.Empty));
        }

        var arguments = new List<string> { "add", "--" };
        arguments.AddRange(paths);
        return _runner.RunAsync(repositoryPath, arguments, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<GitResult> UnstageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return new GitResult(0, string.Empty, string.Empty);
        }

        // コミットがまだ 1 つもないリポジトリでは restore --staged が使えないため、rm --cached で外す
        GitResult head = await _runner.RunAsync(repositoryPath, new[] { "rev-parse", "--verify", "--quiet", "HEAD" }, ct: ct).ConfigureAwait(false);

        var arguments = head.IsSuccess
            ? new List<string> { "restore", "--staged", "--" }
            : new List<string> { "rm", "--cached", "--quiet", "-r", "--" };
        arguments.AddRange(paths);
        return await _runner.RunAsync(repositoryPath, arguments, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<GitResult> CommitAsync(string repositoryPath, string message, bool amend, CancellationToken ct = default)
    {
        // メッセージは引数だと改行や引用符の扱いが難しいため、一時ファイル(UTF-8)経由で渡す
        using var messageFile = new TempMessageFile(message);

        var arguments = new List<string> { "commit" };
        if (amend)
        {
            arguments.Add("--amend");
        }

        arguments.Add("-F");
        arguments.Add(messageFile.Path);

        return await _runner.RunAsync(repositoryPath, arguments, ct: ct).ConfigureAwait(false);
    }
}
