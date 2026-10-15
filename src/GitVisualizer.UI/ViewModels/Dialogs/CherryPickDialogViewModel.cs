using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「チェリーピック」ダイアログの ViewModel。</summary>
public sealed partial class CherryPickDialogViewModel : DialogViewModelBase
{
    [ObservableProperty]
    private bool _recordOrigin;

    [ObservableProperty]
    private bool _noCommit;

    [ObservableProperty]
    private int _mainline = 1;

    /// <summary><see cref="CherryPickDialogViewModel"/> を初期化します。</summary>
    /// <param name="commit">取り込むコミット。</param>
    public CherryPickDialogViewModel(CommitInfo commit)
    {
        Commit = commit;
        MainlineOptions = Enumerable.Range(1, commit.ParentShas.Count).ToList();
    }

    /// <inheritdoc/>
    public override string Title => "チェリーピック";

    /// <summary>取り込むコミット。</summary>
    public CommitInfo Commit { get; }

    /// <summary>マージコミットかどうか(親番号 -m の選択肢を表示する)。</summary>
    public bool IsMergeCommit => Commit.ParentShas.Count > 1;

    /// <summary>親番号の選択肢。</summary>
    public IReadOnlyList<int> MainlineOptions { get; }

    /// <summary>入力内容から <see cref="CherryPickOptions"/> を作ります。</summary>
    /// <returns>cherry-pick 設定。</returns>
    public CherryPickOptions ToOptions() => new()
    {
        RecordOrigin = RecordOrigin,
        NoCommit = NoCommit,
        Mainline = IsMergeCommit ? Mainline : null,
    };
}
