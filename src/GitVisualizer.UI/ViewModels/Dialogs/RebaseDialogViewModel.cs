using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「リベース」ダイアログの ViewModel。</summary>
public sealed partial class RebaseDialogViewModel : DialogViewModelBase
{
    [ObservableProperty]
    private string _onto;

    [ObservableProperty]
    private bool _autoStash;

    /// <summary><see cref="RebaseDialogViewModel"/> を初期化します。</summary>
    /// <param name="currentBranchName">リベースする(現在の)ブランチ名。</param>
    /// <param name="candidates">リベース先の候補(ブランチ名)。</param>
    /// <param name="defaultOnto">リベース先の初期値(ブランチ名またはコミット SHA)。</param>
    public RebaseDialogViewModel(string currentBranchName, IReadOnlyList<string> candidates, string? defaultOnto)
    {
        CurrentBranchName = currentBranchName;
        Candidates = candidates;
        _onto = defaultOnto ?? string.Empty;
    }

    /// <inheritdoc/>
    public override string Title => "リベース";

    /// <summary>リベースする(現在の)ブランチ名。</summary>
    public string CurrentBranchName { get; }

    /// <summary>リベース先の候補(ブランチ名)。</summary>
    public IReadOnlyList<string> Candidates { get; }

    /// <summary>入力内容から <see cref="RebaseOptions"/> を作ります。</summary>
    /// <returns>rebase 設定。</returns>
    public RebaseOptions ToOptions() => new()
    {
        Onto = Onto.Trim(),
        AutoStash = AutoStash,
    };

    /// <inheritdoc/>
    protected override bool CanAccept() => !string.IsNullOrWhiteSpace(Onto);

    partial void OnOntoChanged(string value) => OkCommand.NotifyCanExecuteChanged();
}
