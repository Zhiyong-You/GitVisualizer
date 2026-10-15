using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「Pull」ダイアログの ViewModel。</summary>
public sealed partial class PullDialogViewModel : DialogViewModelBase
{
    [ObservableProperty]
    private PullMode _mode = PullMode.Merge;

    [ObservableProperty]
    private bool _autoStash;

    /// <summary><see cref="PullDialogViewModel"/> を初期化します。</summary>
    /// <param name="branchName">現在のブランチ名。</param>
    /// <param name="upstream">上流ブランチ名。未設定なら <see langword="null"/>。</param>
    public PullDialogViewModel(string branchName, string? upstream)
    {
        BranchName = branchName;
        Upstream = upstream ?? "(上流ブランチが未設定です)";
    }

    /// <inheritdoc/>
    public override string Title => "Pull";

    /// <summary>現在のブランチ名。</summary>
    public string BranchName { get; }

    /// <summary>取り込み元(上流ブランチ)の表示名。</summary>
    public string Upstream { get; }

    /// <summary>入力内容から <see cref="PullOptions"/> を作ります(リモート・ブランチは上流設定に従う)。</summary>
    /// <returns>pull 設定。</returns>
    public PullOptions ToOptions() => new()
    {
        Mode = Mode,
        AutoStash = AutoStash,
    };
}
