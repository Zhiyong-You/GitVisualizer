using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitVisualizer.Vsix.VsServices;

/// <summary>VS のダイアログ(DialogWindow / メッセージボックス)で表示する <see cref="IDialogService"/> 実装。</summary>
internal sealed class VsDialogService : IDialogService
{
    private const string Title = "Git Visualizer";

    private readonly AsyncPackage _package;

    /// <summary><see cref="VsDialogService"/> を初期化します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    public VsDialogService(AsyncPackage package)
    {
        _package = package;
    }

    /// <inheritdoc/>
    public Task<bool> ShowDialogAsync(object viewModel, CancellationToken ct = default)
    {
        // TODO: UI スレッドに切り替え、viewModel の型に対応する View(UI/Views/Dialogs)を Microsoft.VisualStudio.PlatformUI.DialogWindow に載せて ShowModal する
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(string message, CancellationToken ct = default)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        int result = VsShellUtilities.ShowMessageBox(
            _package,
            message,
            Title,
            OLEMSGICON.OLEMSGICON_QUERY,
            OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);
        return result == (int)VSConstants.MessageBoxResult.IDYES;
    }
}
