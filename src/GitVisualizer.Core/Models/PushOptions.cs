namespace GitVisualizer.Core.Models;

/// <summary>push の設定。</summary>
public sealed class PushOptions
{
    /// <summary>リモート名。</summary>
    public string Remote { get; set; } = "origin";

    /// <summary>push するローカルブランチ名。<see langword="null"/> なら現在のブランチ。</summary>
    public string? Branch { get; set; }

    /// <summary>上流ブランチとして設定するかどうか(<c>-u</c>)。</summary>
    public bool SetUpstream { get; set; }

    /// <summary>強制 push するかどうか(<c>--force-with-lease</c>)。</summary>
    public bool ForceWithLease { get; set; }

    /// <summary>タグも push するかどうか(<c>--follow-tags</c>)。</summary>
    public bool IncludeTags { get; set; }
}
