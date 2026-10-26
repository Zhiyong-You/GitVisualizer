namespace GitVisualizer.Core.Models;

/// <summary>unified diff のハンク内の 1 行。</summary>
public sealed class DiffLine
{
    /// <summary><see cref="DiffLine"/> を初期化します。</summary>
    /// <param name="kind">行の種類。</param>
    /// <param name="rawText">先頭の記号を除いた本文。バイト列を Latin1 で 1 バイト = 1 文字にした形(末尾の \r も含む)。</param>
    /// <param name="text">表示用の本文(文字コードを判定してデコードし、末尾の \r を除いたもの)。</param>
    /// <param name="oldLineNumber">変更前の行番号。追加行は <see langword="null"/>。</param>
    /// <param name="newLineNumber">変更後の行番号。削除行は <see langword="null"/>。</param>
    internal DiffLine(DiffLineKind kind, string rawText, string text, int? oldLineNumber, int? newLineNumber)
    {
        Kind = kind;
        RawText = rawText;
        Text = text;
        OldLineNumber = oldLineNumber;
        NewLineNumber = newLineNumber;
    }

    /// <summary>行の種類。</summary>
    public DiffLineKind Kind { get; }

    /// <summary>表示用の本文(先頭の記号は含まない)。</summary>
    public string Text { get; }

    /// <summary>変更前の行番号(1 始まり)。追加行は <see langword="null"/>。</summary>
    public int? OldLineNumber { get; }

    /// <summary>変更後の行番号(1 始まり)。削除行は <see langword="null"/>。</summary>
    public int? NewLineNumber { get; }

    /// <summary>ファイル末尾の改行がない行かどうか(diff の <c>\ No newline at end of file</c>)。</summary>
    public bool NoNewlineAtEnd { get; internal set; }

    /// <summary>追加行または削除行かどうか。</summary>
    public bool IsChange => Kind != DiffLineKind.Context;

    /// <summary>パッチを組み立てるときに使う、元のバイト列のままの本文。</summary>
    internal string RawText { get; }
}
