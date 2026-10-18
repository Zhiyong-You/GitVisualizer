using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>ツールウィンドウ全体の ViewModel。子 ViewModel を束ねる。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRepositoryLocator _repositoryLocator;
    private readonly IRepositoryStateService _repositoryStateService;
    private readonly IOutputLogger _logger;
    private readonly GitOperationContext _context;

    // 再読み込みは同時に 1 つだけ実行し、実行中に要求されたら終了後にもう 1 回だけ実行する
    private Task? _runningRefresh;
    private bool _refreshPending;
    private TaskScheduler? _uiScheduler;

    [ObservableProperty]
    private string? _repositoryPath;

    /// <summary><see cref="MainViewModel"/> を初期化します。</summary>
    /// <param name="branchService">ブランチサービス。</param>
    /// <param name="syncService">同期サービス。</param>
    /// <param name="historyService">履歴サービス。</param>
    /// <param name="commitService">コミットサービス。</param>
    /// <param name="repositoryStateService">リポジトリ状態サービス。</param>
    /// <param name="graphBuilder">コミットグラフの計算器。</param>
    /// <param name="repositoryLocator">操作対象リポジトリのロケーター。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    /// <param name="diffViewerService">差分表示サービス。</param>
    /// <param name="notificationService">通知サービス。</param>
    /// <param name="logger">ログ出力先。</param>
    public MainViewModel(
        IBranchService branchService,
        ISyncService syncService,
        IHistoryService historyService,
        ICommitService commitService,
        IRepositoryStateService repositoryStateService,
        CommitGraphBuilder graphBuilder,
        IRepositoryLocator repositoryLocator,
        IDialogService dialogService,
        IDiffViewerService diffViewerService,
        INotificationService notificationService,
        IOutputLogger logger)
    {
        _repositoryLocator = repositoryLocator;
        _repositoryStateService = repositoryStateService;
        _logger = logger;
        _context = new GitOperationContext(notificationService, logger);

        Toolbar = new ToolbarViewModel(syncService, branchService, dialogService, notificationService, _context);
        BranchTree = new BranchTreeViewModel(branchService, dialogService, _context);
        CommitHistory = new CommitHistoryViewModel(historyService, graphBuilder, dialogService, _context);
        CommitDetail = new CommitDetailViewModel(historyService, diffViewerService, _context);
        WorkingChanges = new WorkingChangesViewModel(commitService, notificationService, _context);
        OperationState = new OperationStateViewModel(historyService, repositoryStateService, dialogService, _context);

        _context.State = OperationState;
        _context.RefreshAll = RefreshAsync;

        CommitHistory.PropertyChanged += OnCommitHistoryPropertyChanged;
        Toolbar.CheckoutRequested += OnToolbarCheckoutRequested;

        // ソリューションの切り替えや、リポジトリのファイル変更(スレッドプールから通知される)で再読み込みする
        _repositoryLocator.RepositoryChanged += (_, _) => RequestRefreshFromAnyThread();
        _repositoryStateService.Changed += (_, _) => RequestRefreshFromAnyThread();
    }

    /// <summary>上部ツールバー。</summary>
    public ToolbarViewModel Toolbar { get; }

    /// <summary>左ペインのブランチツリー。</summary>
    public BranchTreeViewModel BranchTree { get; }

    /// <summary>中央ペインのコミット履歴。</summary>
    public CommitHistoryViewModel CommitHistory { get; }

    /// <summary>右ペインのコミット詳細。</summary>
    public CommitDetailViewModel CommitDetail { get; }

    /// <summary>下部ペインの作業中の変更。</summary>
    public WorkingChangesViewModel WorkingChanges { get; }

    /// <summary>ステータスバー。</summary>
    public OperationStateViewModel OperationState { get; }

    /// <summary>画面全体を再読み込みします。実行中なら終了後にもう一度だけ実行します。UI スレッドから呼ぶこと。</summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>再読み込みが終わると完了するタスク。</returns>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RefreshAsync(CancellationToken ct)
    {
        // この ViewModel はバックグラウンドで生成されるため、UI スレッドのコンテキストは最初の呼び出しで捕捉する
        if (_uiScheduler == null && SynchronizationContext.Current != null)
        {
            _uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

        if (_runningRefresh != null)
        {
            _refreshPending = true;
            return _runningRefresh;
        }

        Task task = RefreshLoopAsync(ct);

        // 同期的に完了した場合は finally で null に戻した後なので、完了済みのタスクを残さない
        if (!task.IsCompleted)
        {
            _runningRefresh = task;
        }

        return task;
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        try
        {
            do
            {
                _refreshPending = false;
                await RefreshCoreAsync(ct);
            }
            while (_refreshPending && !ct.IsCancellationRequested);
        }
        finally
        {
            _runningRefresh = null;
        }
    }

    private async Task RefreshCoreAsync(CancellationToken ct)
    {
        try
        {
            // IRepositoryLocator で操作対象のリポジトリを決める
            string? path = await _repositoryLocator.GetRepositoryRootAsync(ct);
            if (!string.Equals(path, RepositoryPath, StringComparison.OrdinalIgnoreCase))
            {
                RepositoryPath = path;
                _context.RepositoryPath = path;
                _logger.Log(path == null ? "Git リポジトリが見つかりません。" : $"リポジトリ: {path}");
                StartWatching(path);
            }

            if (path == null)
            {
                BranchTree.Clear();
                Toolbar.SetBranches(Enumerable.Empty<BranchInfo>());
                CommitHistory.Clear();
                WorkingChanges.Clear();
                OperationState.CurrentOperation = RepositoryOperation.None;
                OperationState.CurrentBranchName = string.Empty;
                OperationState.StatusMessage = "Git リポジトリが開かれていません。Git で管理されているソリューションを開いてください。";
                return;
            }

            // 各子 ViewModel を再読み込みする
            await BranchTree.RefreshCommand.ExecuteAsync(null);
            Toolbar.SetBranches(BranchTree.LocalBranches.Concat(BranchTree.RemoteBranches));

            BranchInfo? current = BranchTree.CurrentBranch;
            OperationState.CurrentBranchName = current?.Name ?? "(detached HEAD)";
            CommitHistory.CurrentBranchName = OperationState.CurrentBranchName;
            CommitHistory.BranchNames = BranchTree.LocalBranches.Concat(BranchTree.RemoteBranches).Select(b => b.Name).ToList();

            await CommitHistory.RefreshCommand.ExecuteAsync(null);
            CommitInfo? head = CommitHistory.HeadCommit;
            WorkingChanges.HeadCommitMessage = head == null ? null : head.Body.Length == 0 ? head.Subject : head.Subject + "\n\n" + head.Body;

            await WorkingChanges.RefreshCommand.ExecuteAsync(null);
            await OperationState.RefreshCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("リポジトリの読み込み", ex);
        }
    }

    private void StartWatching(string? path)
    {
        try
        {
            _repositoryStateService.Watch(path);
        }
        catch (Exception ex)
        {
            // 監視できなくても手動の再読み込みは使えるので、記録だけする
            _logger.LogError("リポジトリの監視を開始できませんでした。", ex);
        }
    }

    private void RequestRefreshFromAnyThread()
    {
        // WPF のコレクションは UI スレッドでしか変更できないため、UI スレッドで再読み込みする。
        // 最初の再読み込み(画面表示時)より前は、表示時に読み込まれるので何もしない
        if (_uiScheduler != null)
        {
            _ = Task.Factory.StartNew(() => RefreshAsync(CancellationToken.None), CancellationToken.None, TaskCreationOptions.None, _uiScheduler).Unwrap();
        }
    }

    private void OnToolbarCheckoutRequested(object? sender, BranchInfo branch)
    {
        _ = CheckoutFromToolbarAsync(branch);
    }

    private async Task CheckoutFromToolbarAsync(BranchInfo branch)
    {
        try
        {
            await BranchTree.CheckoutCommand.ExecuteAsync(branch);
        }
        catch (Exception ex)
        {
            _logger.LogError("チェックアウトに失敗しました。", ex);
        }

        // キャンセルされた場合も ComboBox を現在のブランチに戻す
        Toolbar.SetBranches(BranchTree.LocalBranches.Concat(BranchTree.RemoteBranches));
    }

    private void OnCommitHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommitHistoryViewModel.SelectedCommit))
        {
            CommitDetail.Commit = CommitHistory.SelectedCommit;
        }
    }
}
