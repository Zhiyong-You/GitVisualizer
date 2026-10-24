namespace GitVisualizer.UI.ViewModels;

/// <summary>コミット履歴に表示する範囲。</summary>
public enum HistoryScope
{
    /// <summary>全ブランチ・リモート・タグ。</summary>
    All,

    /// <summary>現在のブランチ(HEAD)から辿れるコミット。</summary>
    CurrentBranch,

    /// <summary>ブランチツリーで選択したブランチから辿れるコミット(チェックアウト不要)。</summary>
    SelectedBranch,

    /// <summary>現在のブランチと選択したブランチの両方。分岐点や取り込み候補のコミットを確認するのに使う。</summary>
    CurrentAndSelected,
}

/// <summary>表示範囲 ComboBox の選択肢。</summary>
public sealed class HistoryScopeOption
{
    /// <summary><see cref="HistoryScopeOption"/> を初期化します。</summary>
    /// <param name="value">表示範囲。</param>
    /// <param name="label">表示名。</param>
    public HistoryScopeOption(HistoryScope value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>表示範囲。</summary>
    public HistoryScope Value { get; }

    /// <summary>表示名。</summary>
    public string Label { get; }
}
