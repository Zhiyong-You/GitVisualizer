using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;
using GitVisualizer.UI.ViewModels;
using GitVisualizer.Vsix.Commands;
using GitVisualizer.Vsix.VsServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace GitVisualizer.Vsix;

/// <summary>Git Visualizer の VS パッケージ。メニューコマンドの登録とツールウィンドウの組み立てを行う。</summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageIds.PackageGuidString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(GitVisualizerToolWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids80.Outputwindow)]
[ProvideBindingPath]
public sealed class GitVisualizerPackage : AsyncPackage
{
    /// <inheritdoc/>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await ShowToolWindowCommand.InitializeAsync(this);
    }

    /// <inheritdoc/>
    public override IVsAsyncToolWindowFactory? GetAsyncToolWindowFactory(Guid toolWindowType)
    {
        return toolWindowType == typeof(GitVisualizerToolWindow).GUID ? this : null;
    }

    /// <inheritdoc/>
    protected override string GetToolWindowTitle(Type toolWindowType, int id)
    {
        return toolWindowType == typeof(GitVisualizerToolWindow)
            ? GitVisualizerToolWindow.Title
            : base.GetToolWindowTitle(toolWindowType, id);
    }

    /// <summary>
    /// ツールウィンドウ生成前に(バックグラウンドで)呼ばれる。サービスと ViewModel を組み立てて返し、
    /// 戻り値は <see cref="GitVisualizerToolWindow"/> のコンストラクタに渡される(簡易的な手動 DI)。
    /// </summary>
    protected override async System.Threading.Tasks.Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
    {
        IOutputLogger logger = await VsOutputLogger.CreateAsync(this, cancellationToken);

        // Infrastructure
        var gitRunner = new GitProcessRunner(new GitExeLocator(), new GitOutputParser());
        var parser = new GitOutputParser();

        // Core サービス
        var repositoryStateService = new RepositoryStateService(gitRunner);
        var branchService = new BranchService(gitRunner, parser);
        var syncService = new SyncService(gitRunner);
        var historyService = new HistoryService(gitRunner, parser, repositoryStateService, GetEditorHelperPath());
        var commitService = new CommitService(gitRunner, parser);

        // VS 実装
        var repositoryLocator = new VsSolutionRepositoryLocator(this, gitRunner);
        var dialogService = new VsDialogService(this);
        var notificationService = new VsNotificationService(this);

        var viewModel = new MainViewModel(
            branchService,
            syncService,
            historyService,
            commitService,
            repositoryStateService,
            new CommitGraphBuilder(),
            repositoryLocator,
            dialogService,
            notificationService,
            logger);

        logger.Log("Git Visualizer を初期化しました。");
        return viewModel;
    }

    /// <summary>VSIX に同梱した GitEditorHelper.exe のフルパス(拡張機能のインストールフォルダ直下)。</summary>
    private static string GetEditorHelperPath()
    {
        string extensionDirectory = Path.GetDirectoryName(typeof(GitVisualizerPackage).Assembly.Location) ?? string.Empty;
        return Path.Combine(extensionDirectory, "GitEditorHelper.exe");
    }
}
