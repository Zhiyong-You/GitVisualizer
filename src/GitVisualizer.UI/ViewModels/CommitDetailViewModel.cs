using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>右ペインのコミット詳細。</summary>
public sealed partial class CommitDetailViewModel : ObservableObject
{
    private readonly IHistoryService _historyService;
    private readonly IDiffViewerService _diffViewerService;
    private readonly GitOperationContext _context;
    private CancellationTokenSource? _loadCancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommit))]
    private CommitInfo? _commit;

    /// <summary><see cref="CommitDetailViewModel"/> を初期化します。</summary>
    /// <param name="historyService">履歴サービス(変更ファイルと差分の取得に使う)。</param>
    /// <param name="diffViewerService">差分の表示先。</param>
    /// <param name="context">操作対象リポジトリ。</param>
    public CommitDetailViewModel(IHistoryService historyService, IDiffViewerService diffViewerService, GitOperationContext context)
    {
        _historyService = historyService;
        _diffViewerService = diffViewerService;
        _context = context;
    }

    /// <summary>選択中コミットで変更されたファイル。</summary>
    public ObservableCollection<FileChange> Files { get; } = new();

    /// <summary>コミットが選択されているかどうか。</summary>
    public bool HasCommit => Commit != null;

    /// <summary>変更ファイルの、このコミットでの差分を表示します。</summary>
    /// <param name="file">対象の変更ファイル。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>差分ウィンドウを開くと完了するタスク。</returns>
    [RelayCommand]
    private async Task OpenDiffAsync(FileChange? file, CancellationToken ct)
    {
        string? path = _context.RepositoryPath;
        CommitInfo? commit = Commit;
        if (file == null || commit == null || path == null)
        {
            return;
        }

        try
        {
            FileDiff diff = await _historyService.GetCommitFileDiffAsync(path, commit, file, ct);
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

    partial void OnCommitChanged(CommitInfo? value)
    {
        _ = LoadFilesAsync(value);
    }

    private async Task LoadFilesAsync(CommitInfo? commit)
    {
        // 前の読み込みは取り消す(選択を素早く切り替えた場合に古い結果で上書きしない)
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        Files.Clear();

        string? path = _context.RepositoryPath;
        if (commit == null || path == null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _loadCancellation = cts;
        try
        {
            // git show --name-status で変更ファイルを取得する
            IReadOnlyList<FileChange> files = await _historyService.GetCommitChangesAsync(path, commit.Sha, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            foreach (FileChange file in files)
            {
                Files.Add(file);
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
}
