using System;
using System.Collections.Generic;

namespace GitVisualizer.Core.Models;

/// <summary>1 コミット分の情報。</summary>
public sealed class CommitInfo
{
    /// <summary><see cref="CommitInfo"/> を初期化します。</summary>
    /// <param name="sha">コミット SHA(40 桁)。</param>
    /// <param name="parentShas">親コミットの SHA 一覧。マージコミットは 2 つ以上。</param>
    /// <param name="authorName">作成者名。</param>
    /// <param name="authorEmail">作成者メールアドレス。</param>
    /// <param name="authorDate">作成日時。</param>
    /// <param name="subject">コミットメッセージの 1 行目。</param>
    /// <param name="body">コミットメッセージの 2 行目以降。</param>
    /// <param name="refs">このコミットを指す参照名(ブランチ・タグ)。</param>
    public CommitInfo(
        string sha,
        IReadOnlyList<string> parentShas,
        string authorName,
        string authorEmail,
        DateTimeOffset authorDate,
        string subject,
        string body,
        IReadOnlyList<string> refs)
    {
        Sha = sha;
        ParentShas = parentShas;
        AuthorName = authorName;
        AuthorEmail = authorEmail;
        AuthorDate = authorDate;
        Subject = subject;
        Body = body;
        Refs = refs;
    }

    /// <summary>コミット SHA(40 桁)。</summary>
    public string Sha { get; }

    /// <summary>親コミットの SHA 一覧。</summary>
    public IReadOnlyList<string> ParentShas { get; }

    /// <summary>作成者名。</summary>
    public string AuthorName { get; }

    /// <summary>作成者メールアドレス。</summary>
    public string AuthorEmail { get; }

    /// <summary>作成日時。</summary>
    public DateTimeOffset AuthorDate { get; }

    /// <summary>コミットメッセージの 1 行目。</summary>
    public string Subject { get; }

    /// <summary>コミットメッセージの 2 行目以降。</summary>
    public string Body { get; }

    /// <summary>このコミットを指す参照名(ブランチ・タグ)。</summary>
    public IReadOnlyList<string> Refs { get; }
}
