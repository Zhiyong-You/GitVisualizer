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

namespace GitVisualizer.UI.ViewModels;

/// <summary>下部ペインの作業中の変更とコミット入力。</summary>
public sealed partial class WorkingChangesViewModel : ObservableObject
{
    private readonly ICommitService _commitService;
    private readonly INotificationService _notificationService;
    private readonly GitOperationContext _context;

    [ObservableProperty]
    private FileChange? _selectedChange;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private bool _isAmend;

    /// <summary><see cref="WorkingChangesViewModel"/> を初期化します。</summary>
    /// <param name="commitService">コミットサービス。</param>
    /// <param name="notificationService">通知サービス。</param>
    /// <param name="context">操作対象リポジトリと操作の実行手順。</param>
    public WorkingChangesViewModel(ICommitService commitService, INotificationService notificationService, GitOperationContext context)
    {
        _commitService = commitService;
        _notificationService = notificationService;
        _context = context;
    }

    /// <summary>作業ツリーとインデックスの変更ファイル。</summary>
    public ObservableCollection<FileChange> Changes { get; } = new();

    /// <summary>HEAD のコミットメッセージ(Amend にしたときの初期値)。<see cref="MainViewModel"/> が設定する。</summary>
    public string? HeadCommitMessage { get; set; }

    /// <summary>表示を空にします。</summary>
    public void Clear()
    {
        Changes.Clear();
        SelectedChange = null;
    }

    partial void OnIsAmendChanged(bool value)
    {
        // Amend にしたとき、メッセージが空なら直前のコミットのメッセージを入れる
        if (value && string.IsNullOrWhiteSpace(CommitMessage) && HeadCommitMessage != null)
        {
            CommitMessage = HeadCommitMessage.Replace("\n", "\r\n");
        }
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
            IReadOnlyList<FileChange> changes = await _commitService.GetStatusAsync(path, ct);

            // ステージ済みを先に、その中はパス順に並べる
            Changes.Clear();
            foreach (FileChange change in changes.OrderByDescending(c => c.IsStaged).ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase))
            {
                Changes.Add(change);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("変更ファイルの取得", ex);
        }
    }

    [RelayCommand]
    private Task ToggleStageAsync(FileChange? change, CancellationToken ct)
    {
        if (change == null)
        {
            return Task.CompletedTask;
        }

        return change.IsStaged ? UnstageAsync(change, ct) : StageAsync(change, ct);
    }

    [RelayCommand]
    private Task StageAsync(FileChange? change, CancellationToken ct)
    {
        if (change == null || change.IsStaged)
        {
            return Task.CompletedTask;
        }

        return _context.RunAsync($"ステージ ({change.Path})", (path, token) => _commitService.StageAsync(path, new[] { change.Path }, token), ct);
    }

    [RelayCommand]
    private Task UnstageAsync(FileChange? change, CancellationToken ct)
    {
        if (change == null || !change.IsStaged)
        {
            return Task.CompletedTask;
        }

        return _context.RunAsync($"ステージ解除 ({change.Path})", (path, token) => _commitService.UnstageAsync(path, PathsOf(change), token), ct);
    }

    [RelayCommand]
    private Task StageAllAsync(CancellationToken ct)
    {
        List<string> paths = Changes.Where(c => !c.IsStaged).Select(c => c.Path).Distinct().ToList();
        if (paths.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _context.RunAsync("すべてステージ", (path, token) => _commitService.StageAsync(path, paths, token), ct);
    }

    [RelayCommand]
    private Task UnstageAllAsync(CancellationToken ct)
    {
        List<string> paths = Changes.Where(c => c.IsStaged).SelectMany(PathsOf).Distinct().ToList();
        if (paths.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _context.RunAsync("すべてステージ解除", (path, token) => _commitService.UnstageAsync(path, paths, token), ct);
    }

    private bool CanCommit() => !string.IsNullOrWhiteSpace(CommitMessage);

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync(CancellationToken ct)
    {
        if (!IsAmend && !Changes.Any(c => c.IsStaged))
        {
            await _notificationService.ShowErrorAsync("ステージされた変更がありません。コミットするファイルのチェックボックスをオンにしてください。", ct);
            return;
        }

        string message = CommitMessage;
        bool amend = IsAmend;
        bool success = await _context.RunAsync(amend ? "コミット (Amend)" : "コミット", (path, token) => _commitService.CommitAsync(path, message, amend, token), ct);
        if (success)
        {
            CommitMessage = string.Empty;
            IsAmend = false;
        }
    }

    /// <summary>名前変更をステージから外すときは、変更前のパスも対象にする。</summary>
    private static IReadOnlyList<string> PathsOf(FileChange change) =>
        change.OldPath == null ? new[] { change.Path } : new[] { change.Path, change.OldPath };
}
