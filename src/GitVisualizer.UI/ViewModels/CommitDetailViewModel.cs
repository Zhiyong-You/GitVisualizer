using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels;

/// <summary>右ペインのコミット詳細。</summary>
public sealed partial class CommitDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private CommitInfo? _commit;

    /// <summary><see cref="CommitDetailViewModel"/> を初期化します。</summary>
    public CommitDetailViewModel()
    {
        LoadDummyData();
    }

    /// <summary>選択中コミットで変更されたファイル。</summary>
    public ObservableCollection<FileChange> Files { get; } = new();

    partial void OnCommitChanged(CommitInfo? value)
    {
        // TODO: git show --name-status --format= <sha> で value の変更ファイルを取得して Files に反映する
    }

    // TODO: ダミーデータ。実装時に削除する
    private void LoadDummyData()
    {
        Files.Add(new FileChange("src/Login/LoginView.xaml", FileChangeKind.Modified, isStaged: true));
        Files.Add(new FileChange("src/Login/LoginViewModel.cs", FileChangeKind.Added, isStaged: true));
    }
}
