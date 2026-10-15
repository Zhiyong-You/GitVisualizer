using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;

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

        // ソリューション / フォルダを開いた・閉じたときにリポジトリが変わる
        SolutionEvents.OnAfterOpenSolution += (_, _) => RaiseRepositoryChanged();
        SolutionEvents.OnAfterCloseSolution += (_, _) => RaiseRepositoryChanged();
        SolutionEvents.OnAfterOpenFolder += (_, _) => RaiseRepositoryChanged();
        SolutionEvents.OnAfterCloseFolder += (_, _) => RaiseRepositoryChanged();
    }

    /// <inheritdoc/>
    public event EventHandler? RepositoryChanged;

    /// <inheritdoc/>
    public async Task<string?> GetRepositoryRootAsync(CancellationToken ct = default)
    {
        string? directory = await GetSolutionDirectoryAsync(ct);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        // git の実行は UI スレッドを塞がないようにバックグラウンドで行う
        await TaskScheduler.Default;
        GitResult result = await _runner.RunAsync(directory!, new[] { "rev-parse", "--show-toplevel" }, ct: ct);
        if (!result.IsSuccess)
        {
            return null;
        }

        // git は "C:/repo" の形式で返すので、Windows のパス表記にそろえる
        string root = result.StandardOutput.Trim();
        return root.Length == 0 ? null : Path.GetFullPath(root);
    }

    private async Task<string?> GetSolutionDirectoryAsync(CancellationToken ct)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        if (await _package.GetServiceAsync(typeof(SVsSolution)) is not IVsSolution solution)
        {
            return null;
        }

        // フォルダを開いた場合も、そのフォルダがソリューションのディレクトリとして返る
        solution.GetSolutionInfo(out string directory, out _, out _);
        return directory;
    }

    private void RaiseRepositoryChanged()
    {
        RepositoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
