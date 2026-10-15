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

/// <summary>上部ツールバー(Fetch / Pull / Push、ブランチ選択)。</summary>
public sealed partial class ToolbarViewModel : ObservableObject
{
    private readonly ISyncService _syncService;
    private readonly IBranchService _branchService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly GitOperationContext _context;
    private bool _updatingBranches;

    [ObservableProperty]
    private BranchInfo? _selectedBranch;

    /// <summary><see cref="ToolbarViewModel"/> を初期化します。</summary>
    /// <param name="syncService">同期サービス。</param>
    /// <param name="branchService">ブランチサービス。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    /// <param name="notificationService">通知サービス。</param>
    /// <param name="context">操作対象リポジトリと操作の実行手順。</param>
    public ToolbarViewModel(ISyncService syncService, IBranchService branchService, IDialogService dialogService, INotificationService notificationService, GitOperationContext context)
    {
        _syncService = syncService;
        _branchService = branchService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _context = context;
    }

    /// <summary>ComboBox でブランチが選ばれ、切り替えが必要になったときに発生します。</summary>
    public event EventHandler<BranchInfo>? CheckoutRequested;

    /// <summary>ブランチ選択 ComboBox の候補(ローカルブランチ)。</summary>
    public ObservableCollection<BranchInfo> Branches { get; } = new();

    /// <summary>リモート名の一覧(Push ダイアログの候補)。</summary>
    public IReadOnlyList<string> Remotes { get; private set; } = Array.Empty<string>();

    /// <summary>ブランチ一覧を更新します(ComboBox は現在のブランチを選んだ状態にする)。</summary>
    /// <param name="branches">ローカル・リモートのブランチ一覧。</param>
    public void SetBranches(IEnumerable<BranchInfo> branches)
    {
        List<BranchInfo> all = branches.ToList();

        _updatingBranches = true;
        try
        {
            Branches.Clear();
            foreach (BranchInfo branch in all.Where(b => !b.IsRemote))
            {
                Branches.Add(branch);
            }

            SelectedBranch = Branches.FirstOrDefault(b => b.IsCurrent);
        }
        finally
        {
            _updatingBranches = false;
        }

        // "origin/main" → "origin"
        Remotes = all.Where(b => b.IsRemote)
            .Select(b => b.Name.Split('/')[0])
            .Concat(all.Where(b => b.Upstream != null).Select(b => b.Upstream!.Split('/')[0]))
            .Distinct()
            .OrderBy(r => r == "origin" ? 0 : 1)
            .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    partial void OnSelectedBranchChanged(BranchInfo? value)
    {
        // ユーザーが別のブランチを選んだときだけ切り替える(一覧の再読み込みでは切り替えない)
        if (!_updatingBranches && value != null && !value.IsCurrent)
        {
            CheckoutRequested?.Invoke(this, value);
        }
    }

    [RelayCommand]
    private Task FetchAsync(CancellationToken ct)
    {
        return _context.RunAsync("Fetch", (path, progress, token) => _syncService.FetchAsync(path, null, progress, token), ct);
    }

    [RelayCommand]
    private async Task PullAsync(CancellationToken ct)
    {
        BranchInfo? current = Branches.FirstOrDefault(b => b.IsCurrent);
        if (current == null)
        {
            await _notificationService.ShowErrorAsync("ブランチをチェックアウトしていないため Pull できません。", ct);
            return;
        }

        var dialog = new PullDialogViewModel(current.Name, current.Upstream);
        if (!await _dialogService.ShowDialogAsync(dialog, ct))
        {
            return;
        }

        PullOptions options = dialog.ToOptions();
        await _context.RunAsync("Pull", (path, progress, token) => _syncService.PullAsync(path, options, progress, token), ct);
    }

    [RelayCommand]
    private async Task PushAsync(CancellationToken ct)
    {
        BranchInfo? current = Branches.FirstOrDefault(b => b.IsCurrent);
        if (current == null)
        {
            await _notificationService.ShowErrorAsync("ブランチをチェックアウトしていないため Push できません。", ct);
            return;
        }

        if (Remotes.Count == 0)
        {
            await _notificationService.ShowErrorAsync("リモートが登録されていません。先に git remote add でリモートを追加し、Fetch してください。", ct);
            return;
        }

        var dialog = new PushDialogViewModel(Remotes, current.Name, current.Upstream);
        if (!await _dialogService.ShowDialogAsync(dialog, ct))
        {
            return;
        }

        PushOptions options = dialog.ToOptions();
        await _context.RunAsync($"Push ({options.Remote}/{options.Branch})", (path, progress, token) => _syncService.PushAsync(path, options, progress, token), ct);
    }
}
