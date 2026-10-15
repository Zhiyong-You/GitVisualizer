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

    /// <summary>
    /// 指定されたリポジトリのローカルブランチとリモートブランチを取得します。
    /// </summary>
    /// <param name="repositoryPath">対象となる Git リポジトリのルートパス。</param>
    /// <param name="ct">処理をキャンセルするためのトークン。</param>
    /// <returns>取得したブランチ情報の一覧。</returns>
    /// <exception cref="InvalidOperationException">
    /// git for-each-ref の実行に失敗した場合にスローされます。
    /// </exception>
    public async Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(
        string repositoryPath,
        CancellationToken ct = default)
    {
        // ローカルブランチとリモートブランチを取得するための引数を作成する
        var arguments = new[]
        {
        "for-each-ref",
        "refs/heads",
        "refs/remotes",
        "--format=%(HEAD)%00%(refname)%00%(refname:short)%00%(objectname)%00%(upstream:short)%00%(upstream:track)"
    };

        // 指定されたリポジトリで git for-each-ref を実行する
        GitResult result = await _runner.RunAsync(
            repositoryPath,
            arguments,
            ct: ct);

        // git コマンドが失敗した場合は、標準エラーの内容を例外として通知する
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError);
        }

        // 標準出力を BranchInfo の一覧に変換して返す
        return _parser.ParseBranches(result.StandardOutput);
    }

    /// <inheritdoc/>
    public async Task<GitResult> CheckoutAsync(string repositoryPath, BranchInfo branch, CheckoutOptions options, CancellationToken ct = default)
    {
        // 切り替え先に応じた git switch の引数を決める
        List<string> switchArgs = await BuildSwitchArgumentsAsync(repositoryPath, branch, options, ct).ConfigureAwait(false);

        if (options.Handling == ChangeHandling.Discard)
        {
            // 未コミット変更を破棄して切り替える
            switchArgs.Insert(1, "--discard-changes");
        }

        if (options.Handling != ChangeHandling.Stash)
        {
            return await _runner.RunAsync(repositoryPath, switchArgs, ct: ct).ConfigureAwait(false);
        }

        // Stash: 退避 → 切り替え → 復元。退避するものがなかった場合は復元しない
        string? stashBefore = await GetStashTopAsync(repositoryPath, ct).ConfigureAwait(false);
        GitResult push = await _runner.RunAsync(
            repositoryPath,
            new[] { "stash", "push", "--include-untracked", "-m", "GitVisualizer: checkout " + branch.Name },
            ct: ct).ConfigureAwait(false);
        if (!push.IsSuccess)
        {
            return push;
        }

        string? stashAfter = await GetStashTopAsync(repositoryPath, ct).ConfigureAwait(false);
        bool stashed = stashAfter != null && stashAfter != stashBefore;

        GitResult switchResult = await _runner.RunAsync(repositoryPath, switchArgs, ct: ct).ConfigureAwait(false);
        if (!stashed)
        {
            return GitResults.Combine(push, switchResult);
        }

        // 切り替えに失敗しても、退避した変更は元のブランチに戻す
        GitResult pop = await _runner.RunAsync(repositoryPath, new[] { "stash", "pop" }, ct: ct).ConfigureAwait(false);
        return switchResult.IsSuccess
            ? GitResults.Combine(push, switchResult, pop)
            : GitResults.Combine(push, pop, switchResult);
    }

    private async Task<List<string>> BuildSwitchArgumentsAsync(string repositoryPath, BranchInfo branch, CheckoutOptions options, CancellationToken ct)
    {
        if (!branch.IsRemote)
        {
            return new List<string> { "switch", branch.Name };
        }

        if (!options.CreateTrackingBranch)
        {
            // 追跡ブランチを作らない場合は、リモートブランチの先端を detached HEAD でチェックアウトする
            return new List<string> { "switch", "--detach", branch.Name };
        }

        // "origin/feature/x" → ローカル名 "feature/x"
        int slash = branch.Name.IndexOf('/');
        string localName = slash < 0 ? branch.Name : branch.Name.Substring(slash + 1);

        // 同名のローカルブランチが既にあれば、それに切り替える
        GitResult exists = await _runner.RunAsync(
            repositoryPath,
            new[] { "rev-parse", "--verify", "--quiet", "refs/heads/" + localName },
            ct: ct).ConfigureAwait(false);

        return exists.IsSuccess
            ? new List<string> { "switch", localName }
            : new List<string> { "switch", "--create", localName, "--track", branch.Name };
    }

    private async Task<string?> GetStashTopAsync(string repositoryPath, CancellationToken ct)
    {
        GitResult result = await _runner.RunAsync(
            repositoryPath,
            new[] { "rev-parse", "--verify", "--quiet", "refs/stash" },
            ct: ct).ConfigureAwait(false);
        return result.IsSuccess ? result.StandardOutput.Trim() : null;
    }
}
