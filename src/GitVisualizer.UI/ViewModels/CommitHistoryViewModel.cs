using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>中央ペインのコミット履歴。</summary>
public sealed partial class CommitHistoryViewModel : ObservableObject
{
    private readonly IHistoryService _historyService;
    private readonly CommitGraphBuilder _graphBuilder;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private CommitInfo? _selectedCommit;

    /// <summary><see cref="CommitHistoryViewModel"/> を初期化します。</summary>
    /// <param name="historyService">履歴サービス。</param>
    /// <param name="graphBuilder">コミットグラフの計算器。</param>
    /// <param name="dialogService">ダイアログサービス。</param>
    public CommitHistoryViewModel(IHistoryService historyService, CommitGraphBuilder graphBuilder, IDialogService dialogService)
    {
        _historyService = historyService;
        _graphBuilder = graphBuilder;
        _dialogService = dialogService;

        LoadDummyData();
    }

    /// <summary>コミット一覧(新しい順)。</summary>
    public ObservableCollection<CommitInfo> Commits { get; } = new();

    [RelayCommand]
    private Task RefreshAsync(CancellationToken ct)
    {
        // TODO: IHistoryService.GetLogAsync(git log)で取得し、CommitGraphBuilder.Build でグラフを計算する
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task RebaseAsync(CancellationToken ct)
    {
        // TODO: RebaseDialog で RebaseOptions を決めてから IHistoryService.RebaseAsync を呼ぶ(git rebase)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CherryPickAsync(CancellationToken ct)
    {
        // TODO: CherryPickDialog で CherryPickOptions を決めてから IHistoryService.CherryPickAsync を呼ぶ(git cherry-pick)
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task RewordAsync(CancellationToken ct)
    {
        // TODO: RewordDialog で新しいメッセージを受け取り IHistoryService.RewordAsync を呼ぶ
        return Task.CompletedTask;
    }

    // TODO: ダミーデータ。実装時に削除する
    private void LoadDummyData()
    {
        var now = DateTimeOffset.Now;
        Commits.Add(new CommitInfo("a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", new[] { "b2c3d4e5f60718293a4b5c6d7e8f901234567890" }, "Taro Yamada", "taro@example.com", now.AddHours(-1), "ログイン画面のレイアウトを修正", string.Empty, new[] { "HEAD -> main", "origin/main" }));
        Commits.Add(new CommitInfo("b2c3d4e5f60718293a4b5c6d7e8f901234567890", new[] { "c3d4e5f60718293a4b5c6d7e8f90123456789012" }, "Hanako Suzuki", "hanako@example.com", now.AddHours(-5), "README を追加", "ビルド手順を記載。", Array.Empty<string>()));
        Commits.Add(new CommitInfo("c3d4e5f60718293a4b5c6d7e8f90123456789012", Array.Empty<string>(), "Taro Yamada", "taro@example.com", now.AddDays(-1), "Initial commit", string.Empty, Array.Empty<string>()));
    }
}
