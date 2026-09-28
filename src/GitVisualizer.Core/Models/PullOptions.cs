namespace GitVisualizer.Core.Models;

/// <summary>pull の設定。</summary>
public sealed class PullOptions
{
    /// <summary>統合方法。</summary>
    public PullMode Mode { get; set; } = PullMode.Merge;

    /// <summary>リモート名。<see langword="null"/> なら上流設定に従う。</summary>
    public string? Remote { get; set; }

    /// <summary>リモートブランチ名。<see langword="null"/> なら上流設定に従う。</summary>
    public string? Branch { get; set; }

    /// <summary>未コミット変更を自動で退避・復元するかどうか(<c>--autostash</c>)。</summary>
    public bool AutoStash { get; set; }
}
