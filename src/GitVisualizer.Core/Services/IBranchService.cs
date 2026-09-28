using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary>ブランチの取得・切り替え。</summary>
public interface IBranchService
{
    /// <summary>ローカル・リモートのブランチ一覧を取得します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ブランチ一覧。</returns>
    Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>ブランチを切り替えます。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="branch">切り替え先のブランチ。</param>
    /// <param name="options">チェックアウト設定。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>git の実行結果。</returns>
    Task<GitResult> CheckoutAsync(string repositoryPath, BranchInfo branch, CheckoutOptions options, CancellationToken ct = default);
}
