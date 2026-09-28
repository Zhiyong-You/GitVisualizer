using System;
using System.Threading;
using System.Threading.Tasks;

namespace GitVisualizer.UI.Abstractions;

/// <summary>操作対象の Git リポジトリを特定します(VS では開いているソリューションから)。</summary>
public interface IRepositoryLocator
{
    /// <summary>操作対象のリポジトリが変わった(ソリューションを開いた/閉じた)ときに発生します。</summary>
    event EventHandler? RepositoryChanged;

    /// <summary>操作対象のリポジトリのルートパスを取得します。</summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>リポジトリのルートパス。リポジトリ外なら <see langword="null"/>。</returns>
    Task<string?> GetRepositoryRootAsync(CancellationToken ct = default);
}
