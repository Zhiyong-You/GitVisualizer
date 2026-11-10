namespace GitVisualizer.Core.Models;

/// <summary>ファイルの履歴の 1 件(そのファイルを変更したコミットと、そのコミットでの変更内容)。</summary>
public sealed class FileHistoryEntry
{
    /// <summary><see cref="FileHistoryEntry"/> を初期化します。</summary>
    /// <param name="commit">ファイルを変更したコミット。</param>
    /// <param name="file">そのコミットでのファイルの変更(名前変更をたどった、そのコミット時点のパス)。</param>
    public FileHistoryEntry(CommitInfo commit, FileChange file)
    {
        Commit = commit;
        File = file;
    }

    /// <summary>ファイルを変更したコミット。</summary>
    public CommitInfo Commit { get; }

    /// <summary>そのコミットでのファイルの変更。</summary>
    public FileChange File { get; }

    /// <summary>短縮 SHA(7 桁)。</summary>
    public string ShortSha => Commit.Sha.Length > 7 ? Commit.Sha.Substring(0, 7) : Commit.Sha;
}
