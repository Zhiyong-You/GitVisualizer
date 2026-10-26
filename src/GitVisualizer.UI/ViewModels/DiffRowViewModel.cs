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

    /// <summary>削除行と追加行の組。</summary>
    Modified,
}

/// <summary>差分ペインの 1 行(ハンクヘッダー、文脈行、または変更前後の行の組)。</summary>
public sealed class DiffRowViewModel
{
    private DiffRowViewModel(DiffHunk hunk, DiffLine? oldLine, DiffLine? newLine, bool isStaged)
    {
        Hunk = hunk;
        OldLine = oldLine;
        NewLine = newLine;
        IsStaged = isStaged;
        Kind = oldLine == null && newLine == null ? DiffRowKind.HunkHeader
            : oldLine == null ? DiffRowKind.Added
            : newLine == null ? DiffRowKind.Removed
            : oldLine.IsChange ? DiffRowKind.Modified
            : DiffRowKind.Context;
    }

    /// <summary>連続する削除行と追加行を先頭から対応付け、左右表示用の行を作ります。</summary>
    /// <param name="patch">表示する差分。</param>
    /// <returns>ハンクヘッダーと変更前後の行の組。</returns>
    public static IReadOnlyList<DiffRowViewModel> CreateRows(FilePatch patch)
    {
        var rows = new List<DiffRowViewModel>();
        foreach (DiffHunk hunk in patch.Hunks)
        {
            rows.Add(new DiffRowViewModel(hunk, null, null, patch.Change.IsStaged));
            int index = 0;
            while (index < hunk.Lines.Count)
            {
                DiffLine line = hunk.Lines[index];
                if (!line.IsChange)
                {
                    rows.Add(new DiffRowViewModel(hunk, line, line, patch.Change.IsStaged));
                    index++;
                    continue;
                }

                var removed = new List<DiffLine>();
                var added = new List<DiffLine>();
                while (index < hunk.Lines.Count && hunk.Lines[index].IsChange)
                {
                    line = hunk.Lines[index++];
                    (line.Kind == DiffLineKind.Removed ? removed : added).Add(line);
                }

                for (int i = 0; i < System.Math.Max(removed.Count, added.Count); i++)
                {
                    rows.Add(new DiffRowViewModel(
                        hunk,
                        i < removed.Count ? removed[i] : null,
                        i < added.Count ? added[i] : null,
                        patch.Change.IsStaged));
                }
            }
        }

        return rows;
    }

    /// <summary>行が属するハンク。</summary>
    public DiffHunk Hunk { get; }

    /// <summary>変更前の行。追加のみの行とハンクヘッダーでは <see langword="null"/>。</summary>
    public DiffLine? OldLine { get; }

    /// <summary>変更後の行。削除のみの行とハンクヘッダーでは <see langword="null"/>。</summary>
    public DiffLine? NewLine { get; }

    /// <summary>ステージ済みの差分かどうか。</summary>
    public bool IsStaged { get; }

    /// <summary>行のチェックボックスを表示するかどうか。</summary>
    public bool HasChanges => Kind is DiffRowKind.Added or DiffRowKind.Removed or DiffRowKind.Modified;

    /// <summary>変更前の行に削除の背景を表示するかどうか。</summary>
    public bool IsRemoved => OldLine?.Kind == DiffLineKind.Removed;

    /// <summary>変更後の行に追加の背景を表示するかどうか。</summary>
    public bool IsAdded => NewLine?.Kind == DiffLineKind.Added;

    /// <summary>行の種類(背景色の切り替えに使う)。</summary>
    public DiffRowKind Kind { get; }

    /// <summary>変更前の行番号。</summary>
    public string OldNumber => OldLine?.OldLineNumber?.ToString() ?? string.Empty;

    /// <summary>変更後の行番号。</summary>
    public string NewNumber => NewLine?.NewLineNumber?.ToString() ?? string.Empty;

    /// <summary>変更前の本文。</summary>
    public string OldText => OldLine == null ? string.Empty : (IsRemoved ? "-" : " ") + OldLine.Text;

    /// <summary>変更後の本文。</summary>
    public string NewText => NewLine == null ? string.Empty : (IsAdded ? "+" : " ") + NewLine.Text;

    /// <summary>この行を選んだときに適用する追加行・削除行(ハンクヘッダーならハンク内のすべて)。</summary>
    public IEnumerable<DiffLine> ChangeLines => Kind == DiffRowKind.HunkHeader
        ? Hunk.Lines.Where(l => l.IsChange)
        : new[] { OldLine, NewLine }.OfType<DiffLine>().Where(l => l.IsChange);
}
