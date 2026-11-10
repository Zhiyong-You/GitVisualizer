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

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「履歴を表示」ウィンドウ(1 ファイルを変更したコミットの一覧)の ViewModel。</summary>
public sealed partial class FileHistoryViewModel : DialogViewModelBase
{
    private const int MaxCount = 1000;

    private readonly IHistoryService _historyService;
    private readonly IDiffViewerService _diffViewerService;
    private readonly GitOperationContext _context;
    private readonly string _repositoryPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedDiffCommand))]
    private FileHistoryEntry? _selectedEntry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "履歴を読み込んでいます...";

    /// <summary><see cref="FileHistoryViewModel"/> を初期化します。</summary>
    /// <param name="historyService">履歴サービス。</param>
    /// <param name="diffViewerService">差分の表示先。</param>
    /// <param name="context">ログの出力先。</param>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="path">対象ファイルの相対パス。</param>
    public FileHistoryViewModel(IHistoryService historyService, IDiffViewerService diffViewerService, GitOperationContext context, string repositoryPath, string path)
    {
        _historyService = historyService;
        _diffViewerService = diffViewerService;
        _context = context;
        _repositoryPath = repositoryPath;
        Path = path;
    }

    /// <inheritdoc/>
    public override string Title => $"履歴: {Path}";

    /// <summary>対象ファイルの相対パス。</summary>
    public string Path { get; }

    /// <summary>ファイルを変更したコミット(新しい順)。</summary>
    public ObservableCollection<FileHistoryEntry> Entries { get; } = new();

    /// <summary>一覧の代わりに表示するメッセージがあるかどうか。</summary>
    public bool HasMessage => Message.Length != 0;

    /// <summary>履歴を読み込みます。</summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>読み込みが終わると完了するタスク。</returns>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<FileHistoryEntry> entries = await _historyService.GetFileHistoryAsync(_repositoryPath, Path, MaxCount, ct);
            Entries.Clear();
            foreach (FileHistoryEntry entry in entries)
            {
                Entries.Add(entry);
            }

            Message = entries.Count == 0 ? "このファイルを変更したコミットはありません。" : string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = "履歴を取得できませんでした。";
            _context.ReportLoadError("ファイルの履歴の取得", ex);
        }
    }

    private bool CanOpenSelectedDiff() => SelectedEntry != null;

    [RelayCommand(CanExecute = nameof(CanOpenSelectedDiff))]
    private Task OpenSelectedDiffAsync(CancellationToken ct) => OpenDiffAsync(SelectedEntry, ct);

    /// <summary>そのコミットでのファイルの差分を表示します。</summary>
    [RelayCommand]
    private async Task OpenDiffAsync(FileHistoryEntry? entry, CancellationToken ct)
    {
        if (entry == null)
        {
            return;
        }

        try
        {
            FileDiff diff = await _historyService.GetCommitFileDiffAsync(_repositoryPath, entry.Commit, entry.File, ct);
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
}
