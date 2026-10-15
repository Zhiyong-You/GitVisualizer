using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;

namespace GitVisualizer.UI.ViewModels;

/// <summary>右ペインのコミット詳細。</summary>
public sealed partial class CommitDetailViewModel : ObservableObject
{
    private readonly IHistoryService _historyService;
    private readonly GitOperationContext _context;
    private CancellationTokenSource? _loadCancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommit))]
    private CommitInfo? _commit;

    /// <summary><see cref="CommitDetailViewModel"/> を初期化します。</summary>
    /// <param name="historyService">履歴サービス(変更ファイルの取得に使う)。</param>
    /// <param name="context">操作対象リポジトリ。</param>
    public CommitDetailViewModel(IHistoryService historyService, GitOperationContext context)
    {
        _historyService = historyService;
        _context = context;
    }

    /// <summary>選択中コミットで変更されたファイル。</summary>
    public ObservableCollection<FileChange> Files { get; } = new();

    /// <summary>コミットが選択されているかどうか。</summary>
    public bool HasCommit => Commit != null;

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
