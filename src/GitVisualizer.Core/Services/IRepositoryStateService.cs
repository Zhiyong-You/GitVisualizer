using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary>リポジトリの状態(中断中の操作など)を提供します。</summary>
public interface IRepositoryStateService
{
    /// <summary>リポジトリの状態(HEAD、ブランチ、中断中の操作など)が変化したときに発生します。</summary>
    event EventHandler? Changed;

    /// <summary>進行中(中断中)の操作を取得します。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>進行中の操作。なければ <see cref="RepositoryOperation.None"/>。</returns>
    Task<RepositoryOperation> GetOperationAsync(string repositoryPath, CancellationToken ct = default);

    /// <summary>監視するリポジトリを切り替えます。変更があると <see cref="Changed"/> が発生します。</summary>
    /// <param name="repositoryPath">監視するリポジトリのルートパス。<see langword="null"/> なら監視を止めます。</param>
    void Watch(string? repositoryPath);
}
