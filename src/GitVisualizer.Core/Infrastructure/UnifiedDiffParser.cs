using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>
/// <c>git diff</c> の出力(1 ファイル分の unified diff)を <see cref="FilePatch"/> に変換します。
/// </summary>
/// <remarks>
/// パッチを組み立て直して <c>git apply</c> に渡すため、本文は Latin1(1 バイト = 1 文字)で保持して元のバイト列に戻せるようにする。
/// 表示用の文字列は、UTF-8 として正しくなければ Shift-JIS としてデコードする。
/// </remarks>
internal static class UnifiedDiffParser
{
    /// <summary>バイト列と文字列を 1 対 1 で相互変換するためのエンコーディング。</summary>
    public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Regex HunkHeaderRegex = new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.CultureInvariant);

    /// <summary>diff の出力を解析します。</summary>
    /// <param name="output">git diff の標準出力(バイト列のまま)。</param>
    /// <param name="change">diff を取得した変更ファイル。</param>
    /// <returns>解析結果。差分がなければハンクが空の <see cref="FilePatch"/>。</returns>
    public static FilePatch Parse(byte[] output, FileChange change)
    {
        Encoding displayEncoding = DetectDisplayEncoding(output);
        string text = Latin1.GetString(output);

        // 行区切りは \n。CRLF のファイルでは各行の末尾に \r が残るが、パッチに戻すときに必要なので消さない
        string[] rawLines = text.Split('\n');
        int count = rawLines.Length;
        if (count != 0 && rawLines[count - 1].Length == 0)
        {
            count--;
        }

        var headerLines = new List<string>();
        var hunks = new List<DiffHunk>();
        List<DiffLine>? lines = null;
        string? rawHeader = null;
        int oldLine = 0;
        int newLine = 0;

        void FlushHunk()
        {
            if (rawHeader != null && lines != null)
            {
                hunks.Add(new DiffHunk(rawHeader, ToDisplay(rawHeader, displayEncoding), lines));
            }
        }

        for (int i = 0; i < count; i++)
        {
            string raw = rawLines[i];

            if (raw.StartsWith("@@", StringComparison.Ordinal))
            {
                Match match = HunkHeaderRegex.Match(raw);
                if (!match.Success)
                {
                    throw new FormatException($"ハンクヘッダーを解析できません: {raw}");
                }

                FlushHunk();
                rawHeader = raw;
                lines = new List<DiffLine>();
                oldLine = int.Parse(match.Groups[1].Value);
                newLine = int.Parse(match.Groups[2].Value);

                // 0 行目から始まるのは空の側(新規ファイルの変更前など)
                oldLine = Math.Max(oldLine, 1);
                newLine = Math.Max(newLine, 1);
                continue;
            }

            if (lines == null)
            {
                headerLines.Add(raw);
                continue;
            }

            // diff.suppressBlankEmpty が有効だと、空の文脈行は先頭の空白なしで出力される
            char marker = raw.Length == 0 ? ' ' : raw[0];
            string body = raw.Length == 0 ? string.Empty : raw.Substring(1);
            switch (marker)
            {
                case ' ':
                    lines.Add(new DiffLine(DiffLineKind.Context, body, ToDisplay(body, displayEncoding), oldLine++, newLine++));
                    break;
                case '-':
                    lines.Add(new DiffLine(DiffLineKind.Removed, body, ToDisplay(body, displayEncoding), oldLine++, null));
                    break;
                case '+':
                    lines.Add(new DiffLine(DiffLineKind.Added, body, ToDisplay(body, displayEncoding), null, newLine++));
                    break;
                case '\\':
                    // "\ No newline at end of file" は直前の行に付く
                    if (lines.Count != 0)
                    {
                        lines[lines.Count - 1].NoNewlineAtEnd = true;
                    }

                    break;
                default:
                    // 次のファイルの diff(1 ファイルずつ取得するので通常は来ない)
                    FlushHunk();
                    return new FilePatch(change, headerLines, hunks);
            }
        }

        FlushHunk();
        return new FilePatch(change, headerLines, hunks);
    }

    private static Encoding DetectDisplayEncoding(byte[] output)
    {
        try
        {
            StrictUtf8.GetString(output);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            // UTF-8 として不正なら、日本語環境で多い Shift-JIS とみなす
            return Encoding.GetEncoding(932);
        }
    }

    private static string ToDisplay(string raw, Encoding encoding) => encoding.GetString(Latin1.GetBytes(raw)).TrimEnd('\r');
}
