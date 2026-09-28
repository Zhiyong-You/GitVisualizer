namespace GitVisualizer.Core.Models;

/// <summary>作業ツリー・インデックス上のファイル変更の種類。</summary>
public enum FileChangeKind
{
    /// <summary>変更あり。</summary>
    Modified,

    /// <summary>追加。</summary>
    Added,

    /// <summary>削除。</summary>
    Deleted,

    /// <summary>名前変更。</summary>
    Renamed,

    /// <summary>コピー。</summary>
    Copied,

    /// <summary>未追跡。</summary>
    Untracked,

    /// <summary>競合(未マージ)。</summary>
    Conflicted,
}
