using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private FileChange? _selectedChange;

    [ObservableProperty]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private bool _isAmend;

    /// <summary><see cref="WorkingChangesViewModel"/> を初期化します。</summary>
    /// <param name="commitService">コミットサービス。</param>
    /// <param name="notificationService">通知サービス。</param>
    public WorkingChangesViewModel(ICommitService commitService, INotificationService notificationService)
    {
        _commitService = commitService;
        _notificationService = notificationService;

        LoadDummyData();
    }

    /// <summary>作業ツリーとインデックスの変更ファイル。</summary>
    public ObservableCollection<FileChange> Changes { get; } = new();

    [RelayCommand]
    private Task RefreshAsync(CancellationToken ct)
    {
        // TODO: ICommitService.GetStatusAsync(git status)で取得して Changes に反映する
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task StageAsync(FileChange? change, CancellationToken ct)
    {
        // TODO: ICommitService.StageAsync を呼ぶ(git add)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task UnstageAsync(FileChange? change, CancellationToken ct)
    {
        // TODO: ICommitService.UnstageAsync を呼ぶ(git restore --staged)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CommitAsync(CancellationToken ct)
    {
        // TODO: ICommitService.CommitAsync(CommitMessage, IsAmend) を呼ぶ(git commit)
        return Task.CompletedTask;
    }

    // TODO: ダミーデータ。実装時に削除する
    private void LoadDummyData()
    {
        Changes.Add(new FileChange("src/App.xaml.cs", FileChangeKind.Modified, isStaged: true));
        Changes.Add(new FileChange("docs/design.md", FileChangeKind.Untracked, isStaged: false));
        Changes.Add(new FileChange("src/Old.cs", FileChangeKind.Deleted, isStaged: false));
    }
}
