namespace GitVisualizer.Core.Models;

/// <summary>チェックアウト時の未コミット変更の扱い。</summary>
public enum ChangeHandling
{
    /// <summary>変更を持ち越す(競合する場合は失敗)。</summary>
    Keep,

    /// <summary>退避(stash)してから切り替え、切り替え後に戻す。</summary>
    Stash,

    /// <summary>変更を破棄して切り替える。</summary>
    Discard,
}
