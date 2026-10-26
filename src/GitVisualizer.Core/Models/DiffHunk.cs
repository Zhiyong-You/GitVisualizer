using System.Collections.Generic;

namespace GitVisualizer.Core.Models;

/// <summary>unified diff の 1 ハンク(<c>@@ -a,b +c,d @@</c> から次のハンクまで)。</summary>
public sealed class DiffHunk
{
    /// <summary><see cref="DiffHunk"/> を初期化します。</summary>
    /// <param name="rawHeader">ハンクヘッダー行(Latin1 の形)。</param>
    /// <param name="header">表示用のハンクヘッダー行。</param>
    /// <param name="lines">ハンク内の行。</param>
    internal DiffHunk(string rawHeader, string header, IReadOnlyList<DiffLine> lines)
    {
        RawHeader = rawHeader;
        Header = header;
        Lines = lines;
    }

    /// <summary>表示用のハンクヘッダー行(例: <c>@@ -1,5 +1,6 @@ void Main()</c>)。</summary>
    public string Header { get; }

    /// <summary>ハンク内の行。</summary>
    public IReadOnlyList<DiffLine> Lines { get; }

    /// <summary>パッチを組み立てるときに使う、元のバイト列のままのヘッダー行。</summary>
    internal string RawHeader { get; }
}
