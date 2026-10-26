namespace GitVisualizer.Core.Models;

/// <summary>unified diff の 1 行の種類。</summary>
public enum DiffLineKind
{
    /// <summary>変更のない前後の行(文脈行)。</summary>
    Context,

    /// <summary>追加された行(<c>+</c>)。</summary>
    Added,

    /// <summary>削除された行(<c>-</c>)。</summary>
    Removed,
}
