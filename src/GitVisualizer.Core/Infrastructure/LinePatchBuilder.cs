using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>
/// 選択した行だけを含むパッチを組み立てます(<c>git apply --cached --recount</c> に渡す)。
/// </summary>
/// <remarks>
/// パッチの適用先(ステージならインデックス、ステージ解除なら変更後の側)に存在する行は文脈行として残し、存在しない行は捨てる。
/// <list type="table">
///   <listheader><term>方向</term><description>選択していない + 行 / - 行</description></listheader>
///   <item><term>ステージ(reverse = false)</term><description>削除 / 文脈行に変換</description></item>
///   <item><term>ステージ解除(reverse = true)</term><description>文脈行に変換 / 削除</description></item>
/// </list>
/// ハンクヘッダーの行数は合わなくなるが、<c>--recount</c> で git に数え直させる。
/// </remarks>
internal static class LinePatchBuilder
{
    private const string NoNewlineMarker = "\\ No newline at end of file";

    /// <summary>パッチを組み立てます。</summary>
    /// <param name="patch">元の diff。</param>
    /// <param name="selected">適用する追加行・削除行。</param>
    /// <param name="reverse">ステージ解除(<c>git apply --reverse</c>)用に組み立てる場合は <see langword="true"/>。</param>
    /// <returns>パッチのバイト列。適用する行がなければ <see langword="null"/>。</returns>
    public static byte[]? Build(FilePatch patch, IEnumerable<DiffLine> selected, bool reverse)
    {
        var selectedSet = new HashSet<DiffLine>(selected.Where(l => l.IsChange));
        List<DiffLine> allChanges = patch.ChangeLines.ToList();
        selectedSet.IntersectWith(allChanges);
        if (selectedSet.Count == 0)
        {
            return null;
        }

        bool isPartial = selectedSet.Count != allChanges.Count;
        if (isPartial && !patch.CanApplyPartially)
        {
            throw new InvalidOperationException("このファイルは一部の行だけを適用できません。すべての行を選ぶか、ファイル単位で操作してください。");
        }

        var output = new List<string>();

        // すべての行を選んだ場合は元のヘッダーのまま(新規作成・削除もそのまま適用される)。
        // 一部だけの場合は、ファイルが残るので通常の変更としてのヘッダーにする
        output.AddRange(isPartial ? ToModificationHeader(patch.HeaderLines) : patch.HeaderLines);

        foreach (DiffHunk hunk in patch.Hunks)
        {
            if (!hunk.Lines.Any(selectedSet.Contains))
            {
                continue;
            }

            output.Add(hunk.RawHeader);
            foreach (DiffLine line in hunk.Lines)
            {
                string? converted = ConvertLine(line, selectedSet.Contains(line), reverse);
                if (converted == null)
                {
                    continue;
                }

                output.Add(converted);
                if (line.NoNewlineAtEnd)
                {
                    output.Add(NoNewlineMarker);
                }
            }
        }

        var sb = new StringBuilder();
        foreach (string line in output)
        {
            sb.Append(line).Append('\n');
        }

        return UnifiedDiffParser.Latin1.GetBytes(sb.ToString());
    }

    private static string? ConvertLine(DiffLine line, bool isSelected, bool reverse)
    {
        switch (line.Kind)
        {
            case DiffLineKind.Added:
                return isSelected ? "+" + line.RawText : reverse ? " " + line.RawText : null;
            case DiffLineKind.Removed:
                return isSelected ? "-" + line.RawText : reverse ? null : " " + line.RawText;
            default:
                return " " + line.RawText;
        }
    }

    /// <summary>新規作成・削除のヘッダーを、通常の変更のヘッダー(<c>--- a/path</c> / <c>+++ b/path</c>)に書き換えます。</summary>
    private static IEnumerable<string> ToModificationHeader(IReadOnlyList<string> headerLines)
    {
        string? oldPath = headerLines.FirstOrDefault(l => l.StartsWith("--- ", StringComparison.Ordinal))?.Substring(4);
        string? newPath = headerLines.FirstOrDefault(l => l.StartsWith("+++ ", StringComparison.Ordinal))?.Substring(4);

        foreach (string line in headerLines)
        {
            if (line.StartsWith("new file mode ", StringComparison.Ordinal)
                || line.StartsWith("deleted file mode ", StringComparison.Ordinal)
                || line.StartsWith("index ", StringComparison.Ordinal))
            {
                continue;
            }

            if (line == "--- /dev/null" && newPath != null)
            {
                yield return "--- " + SwapPrefix(newPath, 'a');
            }
            else if (line == "+++ /dev/null" && oldPath != null)
            {
                yield return "+++ " + SwapPrefix(oldPath, 'b');
            }
            else
            {
                yield return line;
            }
        }
    }

    /// <summary><c>b/path</c> を <c>a/path</c> のように、パスの接頭辞を差し替えます(引用符付きのパスにも対応)。</summary>
    private static string SwapPrefix(string path, char prefix)
    {
        int start = path.StartsWith("\"", StringComparison.Ordinal) ? 1 : 0;
        return path.Substring(0, start) + prefix + path.Substring(start + 1);
    }
}
