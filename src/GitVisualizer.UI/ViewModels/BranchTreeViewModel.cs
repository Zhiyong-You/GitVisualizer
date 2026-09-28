using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>左ペインのブランチツリー。</summary>
public sealed partial class BranchTreeViewModel : ObservableObject
{
    private readonly IBranchService _branchService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private BranchInfo? _selectedBranch;

    /// <summary><see cref="BranchTreeViewModel"/> を初期化します。</summary>
    /// <param name="branchService">ブランチサービス。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    public BranchTreeViewModel(IBranchService branchService, IDialogService dialogService)
    {
        _branchService = branchService;
        _dialogService = dialogService;

        LoadDummyData();
    }

    /// <summary>ローカルブランチ。</summary>
    public ObservableCollection<BranchInfo> LocalBranches { get; } = new();

    /// <summary>リモート追跡ブランチ。</summary>
    public ObservableCollection<BranchInfo> RemoteBranches { get; } = new();

    [RelayCommand]
    private Task RefreshAsync(CancellationToken ct)
    {
        // TODO: IBranchService.GetBranchesAsync で取得し、IsRemote で LocalBranches / RemoteBranches に振り分ける
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CheckoutAsync(BranchInfo? branch, CancellationToken ct)
    {
        // TODO: CheckoutDialog で CheckoutOptions を決めてから IBranchService.CheckoutAsync を呼ぶ(git switch)
        return Task.CompletedTask;
    }

    // TODO: ダミーデータ。実装時に削除する
    private void LoadDummyData()
    {
        LocalBranches.Add(new BranchInfo("main", "refs/heads/main", "0000000000000000000000000000000000000001", isRemote: false, isCurrent: true, upstream: "origin/main"));
        LocalBranches.Add(new BranchInfo("feature/login", "refs/heads/feature/login", "0000000000000000000000000000000000000002", isRemote: false, isCurrent: false));
        RemoteBranches.Add(new BranchInfo("origin/main", "refs/remotes/origin/main", "0000000000000000000000000000000000000001", isRemote: true, isCurrent: false));
        RemoteBranches.Add(new BranchInfo("origin/develop", "refs/remotes/origin/develop", "0000000000000000000000000000000000000003", isRemote: true, isCurrent: false));
    }
}
