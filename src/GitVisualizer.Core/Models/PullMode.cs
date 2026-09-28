namespace GitVisualizer.Core.Models;

/// <summary>pull 時の統合方法。</summary>
public enum PullMode
{
    /// <summary>マージする(<c>--no-rebase</c>)。</summary>
    Merge,

    /// <summary>リベースする(<c>--rebase</c>)。</summary>
    Rebase,

    /// <summary>fast-forward のみ許可する(<c>--ff-only</c>)。</summary>
    FastForwardOnly,
}
