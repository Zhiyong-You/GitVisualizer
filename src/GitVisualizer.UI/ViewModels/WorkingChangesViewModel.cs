using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;
using GitVisualizer.UI.ViewModels.Dialogs;

namespace GitVisualizer.UI.ViewModels;

/// <summary>下部ペインの作業中の変更とコミット入力。</summary>
public sealed partial class WorkingChangesViewModel : ObservableObject
{
    private const int AmendCandidateCount = 50;

    private readonly ICommitService _commitService;
    private readonly IHistoryService _historyService;
    private readonly IDiffViewerService _diffViewerService;
    private readonly IDocumentService _documentService;
    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;
    private readonly GitOperationContext _context;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(IgnoreFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(IgnoreExtensionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowHistoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowBlameCommand))]
    [NotifyPropertyChangedFor(nameof(IgnoreExtensionHeader))]
    private FileChange? _selectedChange;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private bool _isAmend;

    [ObservableProperty]
    private AmendTarget? _selectedAmendTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStageLines))]
    [NotifyCanExecuteChangedFor(nameof(StageCheckedLinesCommand))]
    private FilePatch? _currentPatch;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StageCheckedLinesCommand))]
    private IReadOnlyList<DiffRowViewModel> _diffRows = Array.Empty<DiffRowViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StageCheckedLinesCommand))]
    private bool _isApplyingDiff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiffMessage))]
    private string _diffMessage = NoSelectionMessage;

    private const string NoSelectionMessage = "変更ファイルを選ぶと差分を表示します。";

    private CancellationTokenSource? _diffLoadCts;

    /// <summary><see cref="WorkingChangesViewModel"/> を初期化します。</summary>
    /// <param name="commitService">コミットサービス。</param>
    /// <param name="historyService">履歴サービス(Amend の対象にできるコミットの取得と、過去のコミットの修正に使う)。</param>
    /// <param name="diffViewerService">差分の表示先。</param>
    /// <param name="documentService">ファイルを開くエディター。</param>
    /// <param name="notificationService">通知サービス。</param>
    /// <param name="dialogService">ダイアログサービス(変更の破棄・履歴の書き換えの確認に使う)。</param>
    /// <param name="context">操作対象リポジトリと操作の実行手順。</param>
    public WorkingChangesViewModel(ICommitService commitService, IHistoryService historyService, IDiffViewerService diffViewerService, IDocumentService documentService, INotificationService notificationService, IDialogService dialogService, GitOperationContext context)
    {
        _commitService = commitService;
        _historyService = historyService;
        _diffViewerService = diffViewerService;
        _documentService = documentService;
        _notificationService = notificationService;
        _dialogService = dialogService;
        _context = context;
    }

    /// <summary>作業ツリーとインデックスの変更ファイル。</summary>
    public ObservableCollection<FileChange> Changes { get; } = new();

    /// <summary>Amend の対象にできるコミット(現在のブランチの新しい順。先頭が HEAD)。</summary>
    public ObservableCollection<AmendTarget> AmendTargets { get; } = new();

    /// <summary>表示中の差分を行単位でステージできるかどうか(未ステージの差分だけ。ステージ解除はファイル単位で行う)。</summary>
    public bool CanStageLines => CurrentPatch is { CanApplyLines: true, Change.IsStaged: false };

    /// <summary>右クリックメニューの「この拡張子を無視」の見出し(拡張子のパターン付き)。</summary>
    public string IgnoreExtensionHeader =>
        SelectedChange != null && GitIgnoreFile.PatternForExtension(SelectedChange.Path) is string pattern
            ? $"この拡張子を無視({pattern.Replace("_", "__")})" : "この拡張子を無視"; // "_" はアクセスキーにならないよう重ねる

    /// <summary>差分の代わりに表示するメッセージがあるかどうか。</summary>
    public bool HasDiffMessage => DiffMessage.Length != 0;

    /// <summary>表示を空にします。</summary>
    public void Clear()
    {
        Changes.Clear();
        SelectedChange = null;
        AmendTargets.Clear();
        SelectedAmendTarget = null;
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

        // 操作や自動更新で同じ差分を読み直したときにチェックが消えないよう、チェック中の行を覚えておく
        HashSet<string?> checkedKeys = CurrentPatch != null && change != null
            && CurrentPatch.Change.Path == change.Path && CurrentPatch.Change.IsStaged == change.IsStaged
            ? new HashSet<string?>(DiffRows.Where(r => r.IsChecked).Select(r => r.Key))
            : new HashSet<string?>();

        CurrentPatch = null;
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
        IReadOnlyList<DiffRowViewModel> rows = DiffRowViewModel.CreateRows(patch);
        foreach (DiffRowViewModel row in rows)
        {
            // ハンクヘッダーのチェックは中の行から決まるので、変更行だけ戻す
            if (row.Key != null)
            {
                row.IsChecked = checkedKeys.Contains(row.Key);
            }

            row.PropertyChanged += (_, _) => StageCheckedLinesCommand.NotifyCanExecuteChanged();
        }

        DiffRows = rows;
    }

    private bool CanStageCheckedLines() => !IsApplyingDiff && CanStageLines && DiffRows.Any(r => r.HasChanges && r.IsChecked);

    [RelayCommand(CanExecute = nameof(CanStageCheckedLines))]
    private async Task StageCheckedLinesAsync(CancellationToken ct)
    {
        FilePatch? patch = CurrentPatch;
        List<DiffRowViewModel> rows = DiffRows.Where(r => r.HasChanges && r.IsChecked).ToList();
        if (!CanStageCheckedLines() || patch == null)
        {
            return;
        }

        List<DiffLine> lines = rows.SelectMany(r => r.ChangeLines).ToList();

        // ステージした行は差分から消え、残りの行は行番号がずれる。読み直しで別の行にチェックが移らないよう先に外す
        foreach (DiffRowViewModel row in rows)
        {
            row.IsChecked = false;
        }

        IsApplyingDiff = true;
        try
        {
            await _context.RunAsync(
                $"チェックした行のステージ ({patch.Change.Path}, {lines.Count} 行)",
                (path, token) => _commitService.StageLinesAsync(path, patch, lines, token),
                ct);
        }
        finally
        {
            IsApplyingDiff = false;
        }
    }

    partial void OnIsAmendChanged(bool value)
    {
        // Amend にしたとき、メッセージが空なら対象のコミットのメッセージを入れる
        if (value && string.IsNullOrWhiteSpace(CommitMessage) && SelectedAmendTarget != null)
        {
            CommitMessage = SelectedAmendTarget.EditableMessage;
        }
    }

    partial void OnSelectedAmendTargetChanged(AmendTarget? oldValue, AmendTarget? newValue)
    {
        // 対象を切り替えたとき、メッセージが空か前の対象のメッセージのままなら、新しい対象のメッセージに入れ替える
        if (!IsAmend || newValue == null || ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CommitMessage) || (oldValue != null && CommitMessage == oldValue.EditableMessage))
        {
            CommitMessage = newValue.EditableMessage;
        }
    }

    private async Task RefreshAmendTargetsAsync(string path, CancellationToken ct)
    {
        IReadOnlyList<CommitInfo> commits = await _historyService.GetLogAsync(path, AmendCandidateCount, new[] { "HEAD" }, ct);
        string? selectedSha = SelectedAmendTarget?.Commit.Sha;

        AmendTargets.Clear();
        for (int i = 0; i < commits.Count; i++)
        {
            AmendTargets.Add(new AmendTarget(commits[i], isHead: i == 0));
        }

        // 選んでいたコミットが残っていれば選び直す(書き換えで SHA が変わったときは直前のコミットに戻す)
        SelectedAmendTarget = AmendTargets.FirstOrDefault(t => t.Commit.Sha == selectedSha) ?? AmendTargets.FirstOrDefault();
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

            await RefreshAmendTargetsAsync(path, ct);
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

    // 削除されたファイルは作業ツリーにないため、開く・削除の対象にしない
    private bool CanOpenFile() => SelectedChange is { Kind: not FileChangeKind.Deleted };

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenFileAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        string? path = _context.RepositoryPath;
        if (!CanOpenFile() || path == null)
        {
            return;
        }

        try
        {
            await _documentService.OpenFileAsync(FullPathOf(path, change!.Path), ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("ファイルを開く処理", ex);
        }
    }

    private bool CanDeleteFile() => CanOpenFile();

    [RelayCommand(CanExecute = nameof(CanDeleteFile))]
    private async Task DeleteFileAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        if (!CanDeleteFile())
        {
            return;
        }

        if (!await _dialogService.ConfirmAsync($"{change!.Path} を削除しますか?\n\nファイルはごみ箱に移動します。", ct))
        {
            return;
        }

        await _context.RunAsync($"削除 ({change.Path})", (path, token) => _commitService.DeleteFileAsync(path, change, token), ct);
    }

    // .gitignore は追跡中のファイルには効かないため、未追跡のファイルだけを対象にする
    private bool CanIgnoreFile() => SelectedChange is { Kind: FileChangeKind.Untracked };

    [RelayCommand(CanExecute = nameof(CanIgnoreFile))]
    private Task IgnoreFileAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        return CanIgnoreFile() ? AddToGitIgnoreAsync(GitIgnoreFile.PatternForPath(change!.Path), ct) : Task.CompletedTask;
    }

    private bool CanIgnoreExtension() => CanIgnoreFile() && GitIgnoreFile.PatternForExtension(SelectedChange!.Path) != null;

    [RelayCommand(CanExecute = nameof(CanIgnoreExtension))]
    private Task IgnoreExtensionAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        return CanIgnoreExtension() ? AddToGitIgnoreAsync(GitIgnoreFile.PatternForExtension(change!.Path)!, ct) : Task.CompletedTask;
    }

    private Task AddToGitIgnoreAsync(string pattern, CancellationToken ct) =>
        _context.RunAsync($".gitignore に追加 ({pattern})", (path, token) => _commitService.AddToGitIgnoreAsync(path, pattern, token), ct);

    // 新しく追加したファイルにはまだ履歴がない。名前変更はコミット済みの変更前のパスで調べる
    private bool CanShowHistory() => SelectedChange is { Kind: not (FileChangeKind.Untracked or FileChangeKind.Added) };

    [RelayCommand(CanExecute = nameof(CanShowHistory))]
    private async Task ShowHistoryAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        string? path = _context.RepositoryPath;
        if (!CanShowHistory() || path == null)
        {
            return;
        }

        var viewModel = new FileHistoryViewModel(_historyService, _diffViewerService, _context, path, CommittedPathOf(change!));
        _ = viewModel.LoadAsync(CancellationToken.None);
        await _dialogService.ShowWindowAsync(viewModel, ct);
    }

    private bool CanShowBlame() => CanShowHistory();

    [RelayCommand(CanExecute = nameof(CanShowBlame))]
    private async Task ShowBlameAsync(CancellationToken ct)
    {
        FileChange? change = SelectedChange;
        string? path = _context.RepositoryPath;
        if (!CanShowBlame() || path == null)
        {
            return;
        }

        try
        {
            string blameFile = await _historyService.WriteBlameAsync(path, CommittedPathOf(change!), ct);
            await _documentService.OpenFileAsync(blameFile, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _context.ReportLoadError("変更履歴(注釈)の取得", ex);
        }
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
        AmendTarget? target = amend ? SelectedAmendTarget : null;
        bool success;
        if (target != null && !target.IsHead)
        {
            if (!await _dialogService.ConfirmAsync(
                    $"コミット {target.ShortSha}「{target.Commit.Subject}」を修正します。\n\n"
                    + "ステージ済みの変更をこのコミットに追加し、メッセージを置き換えます。"
                    + "このコミット以降の履歴が書き換わるため、プッシュ済みの場合は強制プッシュが必要になります。\n\n続行しますか?",
                    ct))
            {
                return;
            }

            success = await _context.RunAsync(
                $"コミット (Amend {target.ShortSha})",
                (path, token) => _historyService.AmendCommitAsync(path, target.Commit.Sha, message, token),
                ct);
        }
        else
        {
            success = await _context.RunAsync(amend ? "コミット (Amend)" : "コミット", (path, token) => _commitService.CommitAsync(path, message, amend, token), ct);
        }

        if (success)
        {
            CommitMessage = string.Empty;
            IsAmend = false;
            SelectedAmendTarget = AmendTargets.FirstOrDefault();
        }
    }

    private static string FullPathOf(string repositoryPath, string relativePath) =>
        System.IO.Path.Combine(repositoryPath, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>コミット済みの履歴を調べるときのパス(名前変更は変更前のパス)。</summary>
    private static string CommittedPathOf(FileChange change) => change.Kind == FileChangeKind.Renamed ? change.OldPath ?? change.Path : change.Path;

    /// <summary>名前変更をステージから外すときは、変更前のパスも対象にする。</summary>
    private static IReadOnlyList<string> PathsOf(FileChange change) =>
        change.OldPath == null ? new[] { change.Path } : new[] { change.Path, change.OldPath };
}
