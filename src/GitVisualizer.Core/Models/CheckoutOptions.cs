namespace GitVisualizer.Core.Models;

/// <summary>ブランチのチェックアウト設定。</summary>
public sealed class CheckoutOptions
{
    /// <summary>未コミット変更の扱い。</summary>
    public ChangeHandling Handling { get; set; } = ChangeHandling.Keep;

    /// <summary>リモートブランチ指定時に、同名のローカル追跡ブランチを作成するかどうか。</summary>
    public bool CreateTrackingBranch { get; set; } = true;
}
