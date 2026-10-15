using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;
using GitVisualizer.UI.Controls;
using GitVisualizer.UI.ViewModels.Dialogs;

namespace GitVisualizer.UI.ViewModels;

/// <summary>中央ペインのコミット履歴。</summary>
public sealed partial class CommitHistoryViewModel : ObservableObject
{
    /// <summary>一度に取得するコミット数の上限。</summary>
    public const int MaxCount = 1000;

    private const double LaneWidth = CommitGraphMetrics.LaneWidth;

    private readonly IHistoryService _historyService;
    private readonly CommitGraphBuilder _graphBuilder;
    private readonly IDialogService _dialogService;
    private readonly GitOperationContext _context;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RebaseCommand), nameof(CherryPickCommand), nameof(RewordCommand))]
    private CommitInfo? _selectedCommit;

    [ObservableProperty]
    private CommitRowViewModel? _selectedRow;

    [ObservableProperty]
    private double _graphColumnWidth = LaneWidth * 2;

    /// <summary><see cref="CommitHistoryViewModel"/> を初期化します。</summary>
    /// <param name="historyService">履歴サービス。</param>
    /// <param name="graphBuilder">コミットグラフの計算器。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    /// <param name="context">操作対象リポジトリと操作の実行手順。</param>
    public CommitHistoryViewModel(IHistoryService historyService, CommitGraphBuilder graphBuilder, IDialogService dialogService, GitOperationContext context)
    {
        _historyService = historyService;
        _graphBuilder = graphBuilder;
        _dialogService = dialogService;
        _context = context;
    }

    /// <summary>コミット一覧(新しい順)。</summary>
    public ObservableCollection<CommitRowViewModel> Commits { get; } = new();

    /// <summary>現在のブランチ名(リベースダイアログに表示)。<see cref="MainViewModel"/> が設定する。</summary>
    public string CurrentBranchName { get; set; } = string.Empty;

    /// <summary>リベース先の候補となるブランチ名。<see cref="MainViewModel"/> が設定する。</summary>
    public IReadOnlyList<string> BranchNames { get; set; } = Array.Empty<string>();

    /// <summary>HEAD のコミット。読み込み前やコミットがない場合は <see langword="null"/>。</summary>
    public CommitInfo? HeadCommit { get; private set; }

    /// <summary>表示を空にします。</summary>
    public void Clear()
    {
        Commits.Clear();
        HeadCommit = null;
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(CommitRowViewModel? value)
    {
        SelectedCommit = value?.Commit;
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
            IReadOnlyList<CommitInfo> commits = await _historyService.GetLogAsync(path, MaxCount, ct);
            IReadOnlyList<GraphNode> nodes = _graphBuilder.Build(commits);

            string? selectedSha = SelectedCommit?.Sha;
            Commits.Clear();
            for (int i = 0; i < commits.Count; i++)
            {
                Commits.Add(new CommitRowViewModel(commits[i], nodes[i]));
            }

            HeadCommit = commits.FirstOrDefault(c => c.Refs.Any(r => r == "HEAD" || r.StartsWith("HEAD -> ", StringComparison.Ordinal)));

            int maxLane = nodes.Count == 0 ? 0 : nodes.Max(n => Math.Max(n.Lane, n.Edges.Count == 0 ? 0 : n.Edges.Max(e => Math.Max(e.FromLane, e.ToLane))));
            GraphColumnWidth = Math.Min(LaneWidth * (maxLane + 1) + 4, 240);

            // 再読み込み前に選んでいたコミットを選び直す
            SelectedRow = selectedSha == null ? null : Commits.FirstOrDefault(r => r.Commit.Sha == selectedSha);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("コミット履歴の取得", ex);
        }
    }

    private bool HasSelection() => SelectedCommit != null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RebaseAsync(CancellationToken ct)
    {
        if (SelectedCommit is not CommitInfo commit)
        {
            return;
        }

        // 選択コミットを指すローカルブランチがあればその名前、なければ SHA をリベース先の初期値にする
        string defaultOnto = commit.Refs
            .Select(r => r.StartsWith("HEAD -> ", StringComparison.Ordinal) ? r.Substring("HEAD -> ".Length) : r)
            .FirstOrDefault(r => r != "HEAD" && BranchNames.Contains(r))
            ?? commit.Sha;

        var dialog = new RebaseDialogViewModel(CurrentBranchName, BranchNames, defaultOnto);
        if (!await _dialogService.ShowDialogAsync(dialog, ct))
        {
            return;
        }

        RebaseOptions options = dialog.ToOptions();
        await _context.RunAsync($"リベース ({options.Onto})", (path, token) => _historyService.RebaseAsync(path, options, token), ct);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task CherryPickAsync(CancellationToken ct)
    {
        if (SelectedCommit is not CommitInfo commit)
        {
            return;
        }

        var dialog = new CherryPickDialogViewModel(commit);
        if (!await _dialogService.ShowDialogAsync(dialog, ct))
        {
            return;
        }

        CherryPickOptions options = dialog.ToOptions();
        await _context.RunAsync(
            $"チェリーピック ({ShortSha(commit)})",
            (path, token) => _historyService.CherryPickAsync(path, new[] { commit.Sha }, options, token),
            ct);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RewordAsync(CancellationToken ct)
    {
        if (SelectedCommit is not CommitInfo commit)
        {
            return;
        }

        var dialog = new RewordDialogViewModel(commit);
        if (!await _dialogService.ShowDialogAsync(dialog, ct))
        {
            return;
        }

        string message = dialog.Message;
        await _context.RunAsync(
            $"コミットメッセージの変更 ({ShortSha(commit)})",
            (path, token) => _historyService.RewordAsync(path, commit.Sha, message, token),
            ct);
    }

    private static string ShortSha(CommitInfo commit) => commit.Sha.Length > 8 ? commit.Sha.Substring(0, 8) : commit.Sha;
}
