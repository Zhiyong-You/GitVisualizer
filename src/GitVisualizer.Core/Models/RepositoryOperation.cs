namespace GitVisualizer.Core.Models;

/// <summary>リポジトリで進行中(中断中)の操作。</summary>
public enum RepositoryOperation
{
    /// <summary>進行中の操作なし。</summary>
    None,

    /// <summary>マージ中(競合で停止)。</summary>
    Merge,

    /// <summary>リベース中。</summary>
    Rebase,

    /// <summary>チェリーピック中。</summary>
    CherryPick,

    /// <summary>リバート中。</summary>
    Revert,
}
