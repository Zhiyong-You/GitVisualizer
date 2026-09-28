using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;

namespace GitVisualizer.UI.ViewModels;

/// <summary>ステータスバー(実行中の処理、中断中の rebase / cherry-pick の続行・中止)。</summary>
public sealed partial class OperationStateViewModel : ObservableObject
{
    private readonly IHistoryService _historyService;
    private readonly IRepositoryStateService _repositoryStateService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOperationInProgress))]
    private RepositoryOperation _currentOperation;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _currentBranchName = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary><see cref="OperationStateViewModel"/> を初期化します。</summary>
    /// <param name="historyService">履歴サービス(continue / skip / abort に使う)。</param>
    /// <param name="repositoryStateService">リポジトリ状態サービス。</param>
    public OperationStateViewModel(IHistoryService historyService, IRepositoryStateService repositoryStateService)
    {
        _historyService = historyService;
        _repositoryStateService = repositoryStateService;

        // TODO: ダミーデータ。実装時に削除する
        StatusMessage = "準備完了(ダミー表示)";
        CurrentBranchName = "main";
    }

    /// <summary>中断中の操作があるかどうか。</summary>
    public bool IsOperationInProgress => CurrentOperation != RepositoryOperation.None;

    [RelayCommand]
    private Task ContinueAsync(CancellationToken ct)
    {
        // TODO: IHistoryService.ContinueAsync を呼ぶ(git rebase --continue / git cherry-pick --continue)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SkipAsync(CancellationToken ct)
    {
        // TODO: IHistoryService.SkipAsync を呼ぶ(git rebase --skip / git cherry-pick --skip)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task AbortAsync(CancellationToken ct)
    {
        // TODO: IHistoryService.AbortAsync を呼ぶ(git rebase --abort / git cherry-pick --abort)
        return Task.CompletedTask;
    }
}
