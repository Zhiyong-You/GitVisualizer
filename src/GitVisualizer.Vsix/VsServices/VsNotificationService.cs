using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitVisualizer.Vsix.VsServices;

/// <summary>VS のステータスバーとメッセージボックスで通知する <see cref="INotificationService"/> 実装。</summary>
internal sealed class VsNotificationService : INotificationService
{
    private const string Title = "Git Visualizer";

    private readonly AsyncPackage _package;

    /// <summary><see cref="VsNotificationService"/> を初期化します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    public VsNotificationService(AsyncPackage package)
    {
        _package = package;
    }

    /// <inheritdoc/>
    public async Task ShowInfoAsync(string message, CancellationToken ct = default)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        if (await _package.GetServiceAsync(typeof(SVsStatusbar)) is IVsStatusbar statusBar)
        {
            statusBar.IsFrozen(out int frozen);
            if (frozen == 0)
            {
                statusBar.SetText(message);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ShowErrorAsync(string message, CancellationToken ct = default)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        VsShellUtilities.ShowMessageBox(
            _package,
            message,
            Title,
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
