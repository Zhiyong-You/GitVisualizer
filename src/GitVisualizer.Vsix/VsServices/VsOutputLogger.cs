using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace GitVisualizer.Vsix.VsServices;

/// <summary>出力ウィンドウの「Git Visualizer」ペインに書き込む <see cref="IOutputLogger"/> 実装。</summary>
internal sealed class VsOutputLogger : IOutputLogger
{
    private const string PaneTitle = "Git Visualizer";

    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly IVsOutputWindowPane _pane;

    private VsOutputLogger(JoinableTaskFactory joinableTaskFactory, IVsOutputWindowPane pane)
    {
        _joinableTaskFactory = joinableTaskFactory;
        _pane = pane;
    }

    /// <summary>出力ウィンドウにペインを作成(既にあれば取得)してロガーを生成します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>生成したロガー。</returns>
    public static async Task<VsOutputLogger> CreateAsync(AsyncPackage package, CancellationToken ct)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        var outputWindow = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow
            ?? throw new InvalidOperationException("SVsOutputWindow を取得できませんでした。");

        Guid paneGuid = new(PackageIds.OutputPaneGuidString);
        ErrorHandler.ThrowOnFailure(outputWindow.CreatePane(ref paneGuid, PaneTitle, fInitVisible: 1, fClearWithSolution: 0));
        ErrorHandler.ThrowOnFailure(outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane pane));
        return new VsOutputLogger(package.JoinableTaskFactory, pane);
    }

    /// <inheritdoc/>
    public void Log(string message)
    {
        Write(message);
    }

    /// <inheritdoc/>
    public void LogError(string message, Exception? exception = null)
    {
        Write(exception is null
            ? $"[エラー] {message}"
            : $"[エラー] {message}{Environment.NewLine}{exception}");
    }

    private void Write(string text)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}";

        // どのスレッドから呼ばれてもよいように UI スレッドへ切り替えて書き込む(UI スレッド上なら同期的に書き込まれる)
        _joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            _pane.OutputStringThreadSafe(line);
        }).FileAndForget("GitVisualizer/OutputLogger");
    }
}
