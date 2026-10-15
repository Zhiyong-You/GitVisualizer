using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「Push」ダイアログの ViewModel。</summary>
public sealed partial class PushDialogViewModel : DialogViewModelBase
{
    [ObservableProperty]
    private string? _remote;

    [ObservableProperty]
    private bool _setUpstream;

    [ObservableProperty]
    private bool _forceWithLease;

    [ObservableProperty]
    private bool _includeTags;

    /// <summary><see cref="PushDialogViewModel"/> を初期化します。</summary>
    /// <param name="remotes">リモート名の候補。</param>
    /// <param name="branchName">push するローカルブランチ名。</param>
    /// <param name="upstream">上流ブランチ名。未設定なら <see langword="null"/>。</param>
    public PushDialogViewModel(IReadOnlyList<string> remotes, string branchName, string? upstream)
    {
        Remotes = remotes;
        BranchName = branchName;

        // 上流のリモートを優先し、なければ origin、それもなければ先頭
        string? upstreamRemote = upstream?.Split('/').FirstOrDefault();
        Remote = remotes.FirstOrDefault(r => r == upstreamRemote)
            ?? remotes.FirstOrDefault(r => r == "origin")
            ?? remotes.FirstOrDefault();

        // 上流が未設定なら、初回 push として上流を設定する
        SetUpstream = upstream == null;
    }

    /// <inheritdoc/>
    public override string Title => "Push";

    /// <summary>リモート名の候補。</summary>
    public IReadOnlyList<string> Remotes { get; }

    /// <summary>push するローカルブランチ名。</summary>
    public string BranchName { get; }

    /// <summary>入力内容から <see cref="PushOptions"/> を作ります。</summary>
    /// <returns>push 設定。</returns>
    public PushOptions ToOptions() => new()
    {
        Remote = Remote ?? "origin",
        Branch = BranchName,
        SetUpstream = SetUpstream,
        ForceWithLease = ForceWithLease,
        IncludeTags = IncludeTags,
    };

    /// <inheritdoc/>
    protected override bool CanAccept() => !string.IsNullOrWhiteSpace(Remote);

    partial void OnRemoteChanged(string? value) => OkCommand.NotifyCanExecuteChanged();
}
