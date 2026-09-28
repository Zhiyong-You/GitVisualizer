using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>上部ツールバー(Fetch / Pull / Push、ブランチ選択)。</summary>
public sealed partial class ToolbarViewModel : ObservableObject
{
    private readonly ISyncService _syncService;
    private readonly IBranchService _branchService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private BranchInfo? _selectedBranch;

    /// <summary><see cref="ToolbarViewModel"/> を初期化します。</summary>
    /// <param name="syncService">同期サービス。</param>
    /// <param name="branchService">ブランチサービス。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    public ToolbarViewModel(ISyncService syncService, IBranchService branchService, IDialogService dialogService)
    {
        _syncService = syncService;
        _branchService = branchService;
        _dialogService = dialogService;

        LoadDummyData();
    }

    /// <summary>ブランチ選択 ComboBox の候補(ローカルブランチ)。</summary>
    public ObservableCollection<BranchInfo> Branches { get; } = new();

    [RelayCommand]
    private Task FetchAsync(CancellationToken ct)
    {
        // TODO: ISyncService.FetchAsync を呼ぶ(git fetch)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task PullAsync(CancellationToken ct)
    {
        // TODO: PullDialog で PullOptions を決めてから ISyncService.PullAsync を呼ぶ(git pull)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task PushAsync(CancellationToken ct)
    {
        // TODO: PushDialog で PushOptions を決めてから ISyncService.PushAsync を呼ぶ(git push)
        return Task.CompletedTask;
    }

    // TODO: ダミーデータ。実装時に IBranchService.GetBranchesAsync の結果に置き換える
    private void LoadDummyData()
    {
        Branches.Add(new BranchInfo("main", "refs/heads/main", "0000000000000000000000000000000000000001", isRemote: false, isCurrent: true, upstream: "origin/main"));
        Branches.Add(new BranchInfo("feature/login", "refs/heads/feature/login", "0000000000000000000000000000000000000002", isRemote: false, isCurrent: false));
        SelectedBranch = Branches[0];
    }
}
