using System;
using System.Collections.Generic;
using GitVisualizer.Core.Graph;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels;

/// <summary>コミット履歴の 1 行(コミットとグラフの描画情報)。</summary>
public sealed class CommitRowViewModel
{
    /// <summary><see cref="CommitRowViewModel"/> を初期化します。</summary>
    /// <param name="commit">コミット。</param>
    /// <param name="node">この行のグラフ。</param>
    public CommitRowViewModel(CommitInfo commit, GraphNode? node)
    {
        Commit = commit;
        Node = node;
    }

    /// <summary>コミット。</summary>
    public CommitInfo Commit { get; }

    /// <summary>この行のグラフ。</summary>
    public GraphNode? Node { get; }

    /// <summary>コミットメッセージの 1 行目。</summary>
    public string Subject => Commit.Subject;

    /// <summary>作成者名。</summary>
    public string AuthorName => Commit.AuthorName;

    /// <summary>作成日時(ローカル時刻)。</summary>
    public DateTimeOffset AuthorDate => Commit.AuthorDate.ToLocalTime();

    /// <summary>短縮 SHA。</summary>
    public string ShortSha => Commit.Sha.Length > 8 ? Commit.Sha.Substring(0, 8) : Commit.Sha;

    /// <summary>このコミットを指す参照名(ブランチ・タグ)。</summary>
    public IReadOnlyList<string> Refs => Commit.Refs;

    /// <summary>参照名があるかどうか。</summary>
    public bool HasRefs => Commit.Refs.Count != 0;
}
