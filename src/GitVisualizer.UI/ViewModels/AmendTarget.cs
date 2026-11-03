using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels;

/// <summary>Amend の対象として選べるコミット(コミット入力欄のドロップダウンの 1 項目)。</summary>
public sealed class AmendTarget
{
    /// <summary><see cref="AmendTarget"/> を初期化します。</summary>
    /// <param name="commit">対象のコミット。</param>
    /// <param name="isHead">直前のコミット(HEAD)かどうか。</param>
    public AmendTarget(CommitInfo commit, bool isHead)
    {
        Commit = commit;
        IsHead = isHead;
    }

    /// <summary>対象のコミット。</summary>
    public CommitInfo Commit { get; }

    /// <summary>直前のコミット(HEAD)かどうか。</summary>
    public bool IsHead { get; }

    /// <summary>短縮 SHA。</summary>
    public string ShortSha => Commit.Sha.Length > 7 ? Commit.Sha.Substring(0, 7) : Commit.Sha;

    /// <summary>コミット入力欄に入れるメッセージ(改行は CRLF)。</summary>
    public string EditableMessage =>
        (Commit.Body.Length == 0 ? Commit.Subject : Commit.Subject + "\n\n" + Commit.Body).Replace("\r\n", "\n").Replace("\n", "\r\n");
}
