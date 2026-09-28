using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>ツールウィンドウ全体の ViewModel。子 ViewModel を束ねる。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRepositoryLocator _repositoryLocator;
    private readonly IOutputLogger _logger;

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
        INotificationService notificationService,
        IOutputLogger logger)
    {
        _repositoryLocator = repositoryLocator;
        _logger = logger;

        Toolbar = new ToolbarViewModel(syncService, branchService, dialogService);
        BranchTree = new BranchTreeViewModel(branchService, dialogService);
        CommitHistory = new CommitHistoryViewModel(historyService, graphBuilder, dialogService);
        CommitDetail = new CommitDetailViewModel();
        WorkingChanges = new WorkingChangesViewModel(commitService, notificationService);
        OperationState = new OperationStateViewModel(historyService, repositoryStateService);

        CommitHistory.PropertyChanged += OnCommitHistoryPropertyChanged;
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

    [RelayCommand]
    private Task RefreshAsync(CancellationToken ct)
    {
        // TODO: IRepositoryLocator.GetRepositoryRootAsync で RepositoryPath を決め、各子 ViewModel の RefreshCommand を実行する
        return Task.CompletedTask;
    }

    private void OnCommitHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommitHistoryViewModel.SelectedCommit))
        {
            CommitDetail.Commit = CommitHistory.SelectedCommit;
        }
    }
}
