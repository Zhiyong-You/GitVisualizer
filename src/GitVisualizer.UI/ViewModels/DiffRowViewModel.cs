using System.Collections.Generic;
using System.Linq;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.ViewModels;

/// <summary>差分ペインの行の種類。</summary>
public enum DiffRowKind
{
    /// <summary>ハンクヘッダー(<c>@@ ... @@</c>)。</summary>
    HunkHeader,

    /// <summary>文脈行。</summary>
    Context,

    /// <summary>追加行。</summary>
    Added,

    /// <summary>削除行。</summary>
    Removed,
}

/// <summary>差分ペインの 1 行(ハンクヘッダーまたは diff の 1 行)。</summary>
public sealed class DiffRowViewModel
{
    /// <summary><see cref="DiffRowViewModel"/> を初期化します。</summary>
    /// <param name="hunk">行が属するハンク。</param>
    /// <param name="line">diff の行。ハンクヘッダーの行は <see langword="null"/>。</param>
    public DiffRowViewModel(DiffHunk hunk, DiffLine? line)
    {
        Hunk = hunk;
        Line = line;
        Kind = line == null ? DiffRowKind.HunkHeader
            : line.Kind == DiffLineKind.Added ? DiffRowKind.Added
            : line.Kind == DiffLineKind.Removed ? DiffRowKind.Removed
            : DiffRowKind.Context;
    }

    /// <summary>行が属するハンク。</summary>
    public DiffHunk Hunk { get; }

    /// <summary>diff の行。ハンクヘッダーの行は <see langword="null"/>。</summary>
    public DiffLine? Line { get; }

    /// <summary>行の種類(背景色の切り替えに使う)。</summary>
    public DiffRowKind Kind { get; }

    /// <summary>変更前の行番号。</summary>
    public string OldNumber => Line?.OldLineNumber?.ToString() ?? string.Empty;

    /// <summary>変更後の行番号。</summary>
    public string NewNumber => Line?.NewLineNumber?.ToString() ?? string.Empty;

    /// <summary>表示する本文(色が分からなくても区別できるよう、+ / - を付ける)。</summary>
    public string Text => Kind switch
    {
        DiffRowKind.HunkHeader => Hunk.Header,
        DiffRowKind.Added => "+" + Line!.Text,
        DiffRowKind.Removed => "-" + Line!.Text,
        _ => " " + Line!.Text,
    };

    /// <summary>この行を選んだときに適用する追加行・削除行(ハンクヘッダーならハンク内のすべて)。</summary>
    public IEnumerable<DiffLine> ChangeLines => Line == null
        ? Hunk.Lines.Where(l => l.IsChange)
        : Line.IsChange ? new[] { Line } : Enumerable.Empty<DiffLine>();
}
