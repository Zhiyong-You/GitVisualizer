using CommunityToolkit.Mvvm.ComponentModel;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>「コミットメッセージ変更」ダイアログの ViewModel。</summary>
public sealed partial class RewordDialogViewModel : DialogViewModelBase
{
    private readonly string _originalMessage;

    [ObservableProperty]
    private string _message;

    /// <summary><see cref="RewordDialogViewModel"/> を初期化します。</summary>
    /// <param name="commit">メッセージを変更するコミット。</param>
    public RewordDialogViewModel(CommitInfo commit)
    {
        Commit = commit;
        _originalMessage = commit.Body.Length == 0 ? commit.Subject : commit.Subject + "\n\n" + commit.Body;

        // TextBox の改行(AcceptsReturn)は CRLF なので合わせておく
        _message = _originalMessage.Replace("\n", "\r\n");
    }

    /// <inheritdoc/>
    public override string Title => "コミットメッセージの変更";

    /// <summary>メッセージを変更するコミット。</summary>
    public CommitInfo Commit { get; }

    /// <inheritdoc/>
    protected override bool CanAccept()
    {
        string normalized = Message.Replace("\r\n", "\n").Trim();
        return normalized.Length != 0 && normalized != _originalMessage.Trim();
    }

    partial void OnMessageChanged(string value) => OkCommand.NotifyCanExecuteChanged();
}
