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
    private readonly IDiffViewerService _diffViewerService;
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;
    private readonly GitOperationContext _context;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private FileChange? _selectedChange;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private bool _isAmend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyLinesLabel))]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectedLinesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiffRowCommand))]
    private FilePatch? _currentPatch;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectedLinesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiffRowCommand))]
    private IReadOnlyList<DiffRowViewModel> _diffRows = Array.Empty<DiffRowViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectedLinesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiffRowCommand))]
    private bool _isApplyingDiff;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectedLinesCommand))]
    private IReadOnlyList<DiffRowViewModel> _selectedDiffRows = Array.Empty<DiffRowViewModel>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiffMessage))]
    private string _diffMessage = NoSelectionMessage;

    private const string NoSelectionMessage = "変更ファイルを選ぶと差分を表示します。";

    private CancellationTokenSource? _diffLoadCts;

    /// <summary><see cref="WorkingChangesViewModel"/> を初期化します。</summary>
    /// <param name="commitService">コミットサービス。</param>
    /// <param name="diffViewerService">差分の表示先。</param>
    /// <param name="notificationService">通知サービス。</param>
    /// <param name="dialogService">ダイアログサービス(変更の破棄の確認に使う)。</param>
    /// <param name="context">操作対象リポジトリと操作の実行手順。</param>
    public WorkingChangesViewModel(ICommitService commitService, IDiffViewerService diffViewerService, INotificationService notificationService, IDialogService dialogService, GitOperationContext context)
    {
        _commitService = commitService;
        _diffViewerService = diffViewerService;
        _notificationService = notificationService;
        _dialogService = dialogService;
        _context = context;
    }

    /// <summary>作業ツリーとインデックスの変更ファイル。</summary>
    public ObservableCollection<FileChange> Changes { get; } = new();

    /// <summary>HEAD のコミットメッセージ(Amend にしたときの初期値)。<see cref="MainViewModel"/> が設定する。</summary>
    public string? HeadCommitMessage { get; set; }

    /// <summary>差分ペインのボタンの文言(ステージ済みのファイルなら解除)。</summary>
    public string ApplyLinesLabel => CurrentPatch?.Change.IsStaged == true ? "選択行をステージ解除" : "選択行をステージ";

    /// <summary>差分の代わりに表示するメッセージがあるかどうか。</summary>
    public bool HasDiffMessage => DiffMessage.Length != 0;

    /// <summary>表示を空にします。</summary>
    public void Clear()
    {
        Changes.Clear();
        SelectedChange = null;
    }

    partial void OnSelectedChangeChanged(FileChange? value)
    {
        _ = LoadDiffAsync(value);
    }

    [RelayCommand]
    private async Task OpenDiffAsync(FileChange? file, CancellationToken ct)
    {
        string? path = _context.RepositoryPath;
        if (file == null || path == null)
        {
            return;
        }

        try
        {
            FileDiff diff = await _commitService.GetFileDiffAsync(path, file, ct);
            await _diffViewerService.ShowDiffAsync(diff, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("差分の表示", ex);
        }
    }

    /// <summary>選択された変更ファイルの diff を読み込み、差分ペインに表示します。</summary>
    private async Task LoadDiffAsync(FileChange? change)
    {
        // 選択を素早く切り替えたときは、前の読み込みを取り消す
        _diffLoadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _diffLoadCts = cts;

        CurrentPatch = null;
        SelectedDiffRows = Array.Empty<DiffRowViewModel>();
        DiffRows = Array.Empty<DiffRowViewModel>();

        string? path = _context.RepositoryPath;
        if (change == null || path == null)
        {
            DiffMessage = NoSelectionMessage;
            return;
        }

        if (change.Kind is FileChangeKind.Renamed or FileChangeKind.Copied or FileChangeKind.Conflicted)
        {
            DiffMessage = "名前変更・コピー・競合中のファイルは行単位で操作できません。チェックボックスでファイル単位で操作してください。";
            return;
        }

        DiffMessage = "差分を読み込んでいます...";
        FilePatch patch;
        try
        {
            patch = await _commitService.GetFilePatchAsync(path, change, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (_diffLoadCts == cts)
            {
                DiffMessage = "差分を取得できませんでした。";
                _context.ReportLoadError("差分の取得", ex);
            }

            return;
        }

        if (_diffLoadCts != cts)
        {
            return;
        }

        CurrentPatch = patch;
        if (!patch.CanApplyLines)
        {
            DiffMessage = patch.IsBinary ? "バイナリファイルは行単位で操作できません。"
                : patch.Hunks.Count == 0 ? "表示できる行の差分はありません(モード変更のみなど)。"
                : "このファイルは行単位で操作できません。";
            return;
        }

        DiffMessage = string.Empty;
        DiffRows = DiffRowViewModel.CreateRows(patch);
    }

    private bool CanApplySelectedLines() => !IsApplyingDiff && CurrentPatch?.CanApplyLines == true && SelectedDiffRows.Any(r => DiffRows.Contains(r) && r.ChangeLines.Any());

    [RelayCommand(CanExecute = nameof(CanApplySelectedLines))]
    private Task ApplySelectedLinesAsync(CancellationToken ct)
    {
        return ApplyLinesAsync(SelectedDiffRows.Where(DiffRows.Contains).SelectMany(r => r.ChangeLines).Distinct().ToList(), ct);
    }

    private bool CanToggleDiffRow(DiffRowViewModel? row) =>
        !IsApplyingDiff && CurrentPatch?.CanApplyLines == true && row?.HasChanges == true && DiffRows.Contains(row);

    [RelayCommand(CanExecute = nameof(CanToggleDiffRow))]
    private Task ToggleDiffRowAsync(DiffRowViewModel? row, CancellationToken ct)
    {
        return CanToggleDiffRow(row) ? ApplyLinesAsync(row!.ChangeLines.ToList(), ct) : Task.CompletedTask;
    }

    private async Task ApplyLinesAsync(IReadOnlyCollection<DiffLine> lines, CancellationToken ct)
    {
        FilePatch? patch = CurrentPatch;
        if (IsApplyingDiff || patch?.CanApplyLines != true || lines.Count == 0)
        {
            return;
        }

        IsApplyingDiff = true;
        try
        {
            bool isStaged = patch.Change.IsStaged;
            string name = $"{(isStaged ? "選択行のステージ解除" : "選択行のステージ")} ({patch.Change.Path}, {lines.Count} 行)";
            await _context.RunAsync(
                name,
                (path, token) => isStaged
                    ? _commitService.UnstageLinesAsync(path, patch, lines, token)
                    : _commitService.StageLinesAsync(path, patch, lines, token),
                ct);
        }
        finally
        {
            IsApplyingDiff = false;
        }
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

            // 一覧を作り直すと選択が外れるため、選んでいたファイルを覚えておく(差分ペインの表示を保つ)
            FileChange? previous = SelectedChange;

            // ステージ済みを先に、その中はパス順に並べる
            Changes.Clear();
            foreach (FileChange change in changes.OrderByDescending(c => c.IsStaged).ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase))
            {
                Changes.Add(change);
            }

            // 同じファイルの同じ側(ステージ済み / 未ステージ)を選び直す。行をすべてステージした後などはもう一方の側を選ぶ
            if (previous != null)
            {
                SelectedChange = Changes.FirstOrDefault(c => c.Path == previous.Path && c.IsStaged == previous.IsStaged)
                    ?? Changes.FirstOrDefault(c => c.Path == previous.Path);
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

    // 追跡中のファイルの変更・削除だけを戻せる(未追跡・追加・名前変更・競合は git restore で元の状態にならない)
    private bool CanRevert() => SelectedChange?.Kind is FileChangeKind.Modified or FileChangeKind.Deleted;

    // コンテキストメニューの CommandParameter は変わっても CanExecute が再評価されないため、選択中のファイルを対象にする(右クリックで選択される)
    [RelayCommand(CanExecute = nameof(CanRevert))]
    private async Task RevertAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        if (!CanRevert())
        {
            return;
        }

        string target = change!.IsStaged ? "ステージ済みの変更と作業ツリーの変更" : "未ステージの変更";
        if (!await _dialogService.ConfirmAsync($"{change.Path} の{target}を破棄して元に戻しますか?\n\nこの操作は取り消せません。", ct))
        {
            return;
        }

        await _context.RunAsync($"変更を元に戻す ({change.Path})", (path, token) => _commitService.RevertAsync(path, change, token), ct);
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
