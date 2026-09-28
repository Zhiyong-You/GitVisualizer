namespace GitVisualizer.Core.Models;

/// <summary>rebase の設定。</summary>
public sealed class RebaseOptions
{
    /// <summary>リベース先(ブランチ名またはコミット SHA)。</summary>
    public string Onto { get; set; } = string.Empty;

    /// <summary>未コミット変更を自動で退避・復元するかどうか(<c>--autostash</c>)。</summary>
    public bool AutoStash { get; set; }
}
