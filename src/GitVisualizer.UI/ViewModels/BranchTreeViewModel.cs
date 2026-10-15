using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;
using GitVisualizer.UI.ViewModels.Dialogs;

namespace GitVisualizer.UI.ViewModels;

/// <summary>左ペインのブランチツリー。</summary>
public sealed partial class BranchTreeViewModel : ObservableObject
{
    private readonly IBranchService _branchService;
    private readonly IDialogService _dialogService;
    private readonly GitOperationContext _context;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutSelectedCommand))]
    private BranchInfo? _selectedBranch;

    /// <summary><see cref="BranchTreeViewModel"/> を初期化します。</summary>
    /// <param name="branchService">ブランチサービス。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    /// <param name="context">操作対象リポジトリと操作の実行手順。</param>
    public BranchTreeViewModel(IBranchService branchService, IDialogService dialogService, GitOperationContext context)
    {
        _branchService = branchService;
        _dialogService = dialogService;
        _context = context;
    }

    /// <summary>ローカルブランチ。</summary>
    public ObservableCollection<BranchInfo> LocalBranches { get; } = new();

    /// <summary>リモート追跡ブランチ。</summary>
    public ObservableCollection<BranchInfo> RemoteBranches { get; } = new();

    /// <summary>現在のブランチ。detached HEAD などで該当がなければ <see langword="null"/>。</summary>
    public BranchInfo? CurrentBranch => LocalBranches.FirstOrDefault(b => b.IsCurrent);

    /// <summary>表示を空にします。</summary>
    public void Clear()
    {
        LocalBranches.Clear();
        RemoteBranches.Clear();
        SelectedBranch = null;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        string? path = _context.RepositoryPath;
        if (path == null)
        {
            Clear();
            return;
        }

        try
        {
            IReadOnlyList<BranchInfo> branches = await _branchService.GetBranchesAsync(path, ct);
            string? selected = SelectedBranch?.FullName;

            LocalBranches.Clear();
            RemoteBranches.Clear();
            foreach (BranchInfo branch in branches)
            {
                (branch.IsRemote ? RemoteBranches : LocalBranches).Add(branch);
            }

            SelectedBranch = branches.FirstOrDefault(b => b.FullName == selected);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("ブランチ一覧の取得", ex);
        }
    }

    private bool CanCheckoutSelected() => SelectedBranch is { } branch && !(branch.IsCurrent && !branch.IsRemote);

    [RelayCommand(CanExecute = nameof(CanCheckoutSelected))]
    private Task CheckoutSelectedAsync(CancellationToken ct) => CheckoutAsync(SelectedBranch, ct);

    [RelayCommand]
    private async Task CheckoutAsync(BranchInfo? branch, CancellationToken ct)
    {
        // 現在のブランチへの切り替えは何もしない
        if (branch == null || (branch.IsCurrent && !branch.IsRemote))
        {
            return;
        }

        var dialog = new CheckoutDialogViewModel(branch);
        if (!await _dialogService.ShowDialogAsync(dialog, ct))
        {
            return;
        }

        CheckoutOptions options = dialog.ToOptions();
        await _context.RunAsync($"チェックアウト ({branch.Name})", (path, token) => _branchService.CheckoutAsync(path, branch, options, token), ct);
    }
}
