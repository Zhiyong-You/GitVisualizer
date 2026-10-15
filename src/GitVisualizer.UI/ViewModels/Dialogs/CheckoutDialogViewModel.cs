using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「チェックアウト」ダイアログの ViewModel。</summary>
public sealed partial class CheckoutDialogViewModel : DialogViewModelBase
{
    [ObservableProperty]
    private ChangeHandling _handling = ChangeHandling.Keep;

    [ObservableProperty]
    private bool _createTrackingBranch = true;

    /// <summary><see cref="CheckoutDialogViewModel"/> を初期化します。</summary>
    /// <param name="branch">切り替え先のブランチ。</param>
    public CheckoutDialogViewModel(BranchInfo branch)
    {
        Branch = branch;
    }

    /// <inheritdoc/>
    public override string Title => "チェックアウト";

    /// <summary>切り替え先のブランチ。</summary>
    public BranchInfo Branch { get; }

    /// <summary>リモートブランチかどうか(追跡ブランチ作成の選択肢を表示する)。</summary>
    public bool IsRemote => Branch.IsRemote;

    /// <summary>入力内容から <see cref="CheckoutOptions"/> を作ります。</summary>
    /// <returns>チェックアウト設定。</returns>
    public CheckoutOptions ToOptions() => new()
    {
        Handling = Handling,
        CreateTrackingBranch = CreateTrackingBranch,
    };
}
