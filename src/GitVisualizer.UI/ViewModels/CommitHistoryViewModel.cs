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

    private const string HeadRevision = "HEAD";

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

    /// <summary>選択したコミットの線(ブランチの先端から第 1 親を辿った経路)。未選択なら <see langword="null"/>。</summary>
    [ObservableProperty]
    private GraphHighlight? _graphHighlight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiltered), nameof(IsBranchFiltered))]
    private HistoryScope _scope = HistoryScope.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBranchFiltered))]
    private BranchInfo? _viewBranch;

    // 表示範囲の変更で再読み込みしない(MainViewModel の再読み込み中に呼ばれる場合)
    private bool _suppressReload;

    // 読み込みのたびに増やし、後から始まった読み込みがあれば古い結果を捨てる
    private int _loadVersion;

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

    /// <summary>表示範囲の選択肢。</summary>
    public IReadOnlyList<HistoryScopeOption> ScopeOptions { get; } = new[]
    {
        new HistoryScopeOption(HistoryScope.All, "すべて"),
        new HistoryScopeOption(HistoryScope.CurrentBranch, "現在のブランチ"),
        new HistoryScopeOption(HistoryScope.SelectedBranch, "選択ブランチ"),
        new HistoryScopeOption(HistoryScope.CurrentAndSelected, "現在 + 選択"),
    };

    /// <summary>全ブランチ以外に絞り込んで表示しているかどうか。</summary>
    public bool IsFiltered => Scope != HistoryScope.All;

    /// <summary>選択ブランチの履歴を(現在のブランチと合わせて、を含む)表示しているかどうか。</summary>
    public bool IsBranchFiltered => ShowsSelectedBranch && ViewBranch != null;

    private bool ShowsSelectedBranch => Scope is HistoryScope.SelectedBranch or HistoryScope.CurrentAndSelected;

    /// <summary>表示を空にします。</summary>
    public void Clear()
    {
        _loadVersion++;
        Commits.Clear();
        HeadCommit = null;
        SelectedRow = null;
    }

    /// <summary>
    /// ブランチツリーで選択したブランチの履歴を表示します(チェックアウトはしない)。
    /// 「現在 + 選択」を表示中ならその表示範囲のまま、選択ブランチだけを切り替えます。
    /// </summary>
    /// <param name="branch">選択したブランチ。グループ見出しを選んだ場合は <see langword="null"/> で、表示は変えない。</param>
    public void ShowBranch(BranchInfo? branch)
    {
        if (branch == null || (ShowsSelectedBranch && branch.FullName == ViewBranch?.FullName))
        {
            return;
        }

        ViewBranch = branch;
        if (ShowsSelectedBranch)
        {
            Reload();
        }
        else
        {
            Scope = HistoryScope.SelectedBranch;
        }
    }

    /// <summary>
    /// 再読み込み後のブランチ一覧に合わせて、表示中のブランチを最新の情報に置き換えます。
    /// ブランチが削除されていたら、選択ブランチの表示は全ブランチに、「現在 + 選択」は現在のブランチに戻します。
    /// 履歴の再読み込みは呼び出し側で行います。
    /// </summary>
    /// <param name="branches">最新のブランチ一覧。</param>
    public void UpdateViewBranch(IEnumerable<BranchInfo> branches)
    {
        if (ViewBranch is not BranchInfo current)
        {
            return;
        }

        _suppressReload = true;
        try
        {
            ViewBranch = branches.FirstOrDefault(b => b.FullName == current.FullName);
            if (ViewBranch == null)
            {
                Scope = Scope switch
                {
                    HistoryScope.SelectedBranch => HistoryScope.All,
                    HistoryScope.CurrentAndSelected => HistoryScope.CurrentBranch,
                    _ => Scope,
                };
            }
        }
        finally
        {
            _suppressReload = false;
        }
    }

    partial void OnScopeChanged(HistoryScope value)
    {
        Reload();
    }

    private void Reload()
    {
        if (!_suppressReload)
        {
            _ = RefreshCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void ShowAll()
    {
        Scope = HistoryScope.All;
    }

    partial void OnSelectedRowChanged(CommitRowViewModel? value)
    {
        SelectedCommit = value?.Commit;
        GraphHighlight = TraceHighlight(value);
    }

    private GraphHighlight? TraceHighlight(CommitRowViewModel? row)
    {
        int index = row == null ? -1 : Commits.IndexOf(row);
        if (index < 0 || row!.Node == null)
        {
            return null;
        }

        return GraphHighlight.Trace(
            Commits.Select(r => r.Commit).ToList(),
            Commits.Select(r => r.Node!).ToList(),
            index);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync(CancellationToken ct)
    {
        string? path = _context.RepositoryPath;
        if (path == null)
        {
            Clear();
            return;
        }

        int version = ++_loadVersion;
        try
        {
            IReadOnlyList<string>? revisions = GetRevisions();
            IReadOnlyList<CommitInfo> commits = await _historyService.GetLogAsync(path, MaxCount, revisions, ct);

            // HEAD は作業中の変更(Amend)でも使うため、表示中の履歴に含まれなければ別に取得する
            CommitInfo? head = FindHead(commits);
            if (head == null && revisions != null && !revisions.Contains(HeadRevision))
            {
                head = FindHead(await _historyService.GetLogAsync(path, 1, new[] { HeadRevision }, ct));
            }

            if (version != _loadVersion)
            {
                return;
            }

            IReadOnlyList<GraphNode> nodes = _graphBuilder.Build(commits);

            string? selectedSha = SelectedCommit?.Sha;
            Commits.Clear();
            for (int i = 0; i < commits.Count; i++)
            {
                Commits.Add(new CommitRowViewModel(commits[i], nodes[i]));
            }

            HeadCommit = head;

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

    /// <summary>表示範囲に応じた git log の起点。<see langword="null"/> は全ブランチ。</summary>
    private IReadOnlyList<string>? GetRevisions()
    {
        string? selected = ViewBranch?.FullName;
        return Scope switch
        {
            HistoryScope.CurrentBranch => new[] { HeadRevision },
            HistoryScope.SelectedBranch when selected != null => new[] { selected },
            HistoryScope.CurrentAndSelected => selected == null ? new[] { HeadRevision } : new[] { HeadRevision, selected },
            _ => null,
        };
    }

    private static CommitInfo? FindHead(IReadOnlyList<CommitInfo> commits) =>
        commits.FirstOrDefault(c => c.Refs.Any(r => r == "HEAD" || r.StartsWith("HEAD -> ", StringComparison.Ordinal)));

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
