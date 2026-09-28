namespace GitVisualizer.Core.Models;

/// <summary>cherry-pick の設定。</summary>
public sealed class CherryPickOptions
{
    /// <summary>コミットせずに変更だけ取り込むかどうか(<c>--no-commit</c>)。</summary>
    public bool NoCommit { get; set; }

    /// <summary>元コミットの SHA をメッセージに追記するかどうか(<c>-x</c>)。</summary>
    public bool RecordOrigin { get; set; }

    /// <summary>マージコミットを取り込む場合の親番号(<c>-m</c>)。通常は <see langword="null"/>。</summary>
    public int? Mainline { get; set; }
}
