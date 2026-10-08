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
    public Task<GitResult> CheckoutAsync(string repositoryPath, BranchInfo branch, CheckoutOptions options, CancellationToken ct = default)
    {
        // TODO: git switch <name>(リモートなら --track)を実行。未コミット変更の扱いは options.Handling に従う(Stash: git stash push/pop、Discard: --discard-changes)
        throw new NotImplementedException();
    }
}
