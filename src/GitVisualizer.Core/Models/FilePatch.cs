using System.Collections.Generic;
using System.Linq;

namespace GitVisualizer.Core.Models;

/// <summary>
/// 作業ツリーまたはインデックスの 1 ファイル分の unified diff。行単位のステージ / ステージ解除に使う。
/// </summary>
public sealed class FilePatch
{
    /// <summary><see cref="FilePatch"/> を初期化します。</summary>
    /// <param name="change">diff を取得した変更ファイル。</param>
    /// <param name="headerLines">最初のハンクより前の行(<c>diff --git</c>、<c>---</c>、<c>+++</c> など。Latin1 の形)。</param>
    /// <param name="hunks">ハンク。</param>
    internal FilePatch(FileChange change, IReadOnlyList<string> headerLines, IReadOnlyList<DiffHunk> hunks)
    {
        Change = change;
        HeaderLines = headerLines;
        Hunks = hunks;
        IsBinary = headerLines.Any(l => l.StartsWith("Binary files ") || l == "GIT binary patch");
        IsCombined = headerLines.Count != 0 && (headerLines[0].StartsWith("diff --cc ") || headerLines[0].StartsWith("diff --combined "));
        IsNewFile = headerLines.Any(l => l.StartsWith("new file mode "));
        IsDeletedFile = headerLines.Any(l => l.StartsWith("deleted file mode "));
    }

    /// <summary>diff を取得した変更ファイル。</summary>
    public FileChange Change { get; }

    /// <summary>ハンク。</summary>
    public IReadOnlyList<DiffHunk> Hunks { get; }

    /// <summary>バイナリファイルの差分かどうか。</summary>
    public bool IsBinary { get; }

    /// <summary>競合中のファイルの結合差分(<c>diff --cc</c>)かどうか。</summary>
    public bool IsCombined { get; }

    /// <summary>新規ファイルの差分かどうか。</summary>
    public bool IsNewFile { get; }

    /// <summary>ファイル削除の差分かどうか。</summary>
    public bool IsDeletedFile { get; }

    /// <summary>行単位でステージ / ステージ解除できるかどうか。</summary>
    public bool CanApplyLines => !IsBinary && !IsCombined && Hunks.Count != 0;

    /// <summary>
    /// 一部の行だけを選んで適用できるかどうか。
    /// ステージ済みの削除はインデックスにファイルがないため、すべての行を選んだ場合(= ファイル単位)だけ適用できる。
    /// </summary>
    public bool CanApplyPartially => CanApplyLines && !(Change.IsStaged && IsDeletedFile);

    /// <summary>最初のハンクより前の行(Latin1 の形)。</summary>
    internal IReadOnlyList<string> HeaderLines { get; }

    /// <summary>すべての追加行・削除行。</summary>
    public IEnumerable<DiffLine> ChangeLines => Hunks.SelectMany(h => h.Lines).Where(l => l.IsChange);
}
