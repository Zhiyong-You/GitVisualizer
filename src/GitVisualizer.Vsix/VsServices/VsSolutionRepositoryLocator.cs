using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio.Shell;

namespace GitVisualizer.Vsix.VsServices;

/// <summary>VS で開いているソリューション(またはフォルダ)から Git リポジトリを特定する <see cref="IRepositoryLocator"/> 実装。</summary>
internal sealed class VsSolutionRepositoryLocator : IRepositoryLocator
{
    private readonly AsyncPackage _package;
    private readonly IGitCommandRunner _runner;

    /// <summary><see cref="VsSolutionRepositoryLocator"/> を初期化します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    /// <param name="runner">git 実行器。</param>
    public VsSolutionRepositoryLocator(AsyncPackage package, IGitCommandRunner runner)
    {
        _package = package;
        _runner = runner;
    }

    // TODO: 実装時に削除(未使用イベントの警告 CS0067 を抑止している)。
    //       Microsoft.VisualStudio.Shell.Events.SolutionEvents の OnAfterOpenSolution / OnAfterCloseSolution で発火させる
#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler? RepositoryChanged;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public Task<string?> GetRepositoryRootAsync(CancellationToken ct = default)
    {
        // TODO: IVsSolution.GetSolutionInfo でソリューションのフォルダを取得し、そこで git rev-parse --show-toplevel を実行する
        throw new NotImplementedException();
    }
}
