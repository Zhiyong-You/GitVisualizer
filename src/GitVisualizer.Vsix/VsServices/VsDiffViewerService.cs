using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitVisualizer.Vsix.VsServices;

/// <summary>VS の差分ビューアー(<see cref="IVsDifferenceService"/>)で表示する <see cref="IDiffViewerService"/> 実装。</summary>
internal sealed class VsDiffViewerService : IDiffViewerService
{
    private readonly AsyncPackage _package;

    /// <summary><see cref="VsDiffViewerService"/> を初期化します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    public VsDiffViewerService(AsyncPackage package)
    {
        _package = package;
    }

    /// <inheritdoc/>
    public async Task ShowDiffAsync(FileDiff diff, CancellationToken ct = default)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        if (await _package.GetServiceAsync(typeof(SVsDifferenceService)) is not IVsDifferenceService differenceService)
        {
            throw new InvalidOperationException("Visual Studio の差分ビューアーを取得できませんでした。");
        }

        // 一時ファイルとして開く(最近使ったファイルに載らず、保存も求められない)。バイナリは差分の代わりにその旨を表示する
        uint options = (uint)(__VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary
            | __VSDIFFSERVICEOPTIONS.VSDIFFOPT_RightFileIsTemporary
            | __VSDIFFSERVICEOPTIONS.VSDIFFOPT_DetectBinaryFiles);

        IVsWindowFrame frame = differenceService.OpenComparisonWindow2(
            diff.LeftFile,
            diff.RightFile,
            diff.Caption,
            diff.Caption,
            diff.LeftLabel,
            diff.RightLabel,
            diff.RightLabel,
            null,
            options);
        ErrorHandler.ThrowOnFailure(frame.Show());
    }
}
