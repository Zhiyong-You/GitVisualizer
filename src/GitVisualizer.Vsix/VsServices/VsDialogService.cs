using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GitVisualizer.UI.Abstractions;
using GitVisualizer.UI.ViewModels.Dialogs;
using GitVisualizer.UI.Views.Dialogs;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
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
    public async Task<bool> ShowDialogAsync(object viewModel, CancellationToken ct = default)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        if (viewModel is not DialogViewModelBase dialogViewModel)
        {
            throw new ArgumentException($"ダイアログの ViewModel ではありません: {viewModel.GetType().FullName}", nameof(viewModel));
        }

        UserControl view = CreateView(dialogViewModel);
        view.DataContext = dialogViewModel;

        var window = new DialogWindow
        {
            Title = $"{dialogViewModel.Title} - {Title}",
            Content = view,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            HasMinimizeButton = false,
            HasMaximizeButton = false,
            ShowInTaskbar = false,
        };
        window.SetResourceReference(Control.BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);

        // ViewModel の OK / キャンセルでウィンドウを閉じる
        void OnCloseRequested(object? sender, bool accepted)
        {
            // IsCancel のボタンなどで既に閉じている場合は何もしない
            if (window.IsVisible)
            {
                window.DialogResult = accepted;
            }
        }

        dialogViewModel.CloseRequested += OnCloseRequested;
        try
        {
            // ShowModal は VS のメインウィンドウを親にして表示する
            return window.ShowModal() == true;
        }
        finally
        {
            dialogViewModel.CloseRequested -= OnCloseRequested;
        }
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

    /// <summary>ViewModel の型に対応する View(UI/Views/Dialogs)を作ります。</summary>
    private static UserControl CreateView(DialogViewModelBase viewModel) => viewModel switch
    {
        CheckoutDialogViewModel => new CheckoutDialog(),
        PullDialogViewModel => new PullDialog(),
        PushDialogViewModel => new PushDialog(),
        RebaseDialogViewModel => new RebaseDialog(),
        CherryPickDialogViewModel => new CherryPickDialog(),
        RewordDialogViewModel => new RewordDialog(),
        _ => throw new ArgumentException($"対応する View がありません: {viewModel.GetType().FullName}", nameof(viewModel)),
    };
}
