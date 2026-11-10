using System;
using System.IO;
using System.Linq;
using System.Text;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>リポジトリ直下の .gitignore へのパターンの追加。</summary>
public static class GitIgnoreFile
{
    /// <summary>そのファイルだけを無視するパターン(ルートからの絶対パターン。例: <c>/src/a.txt</c>)を作ります。</summary>
    /// <param name="relativePath">リポジトリルートからの相対パス(区切りは "/")。</param>
    /// <returns>.gitignore に書くパターン。</returns>
    public static string PatternForPath(string relativePath) => "/" + Escape(relativePath.TrimStart('/'));

    /// <summary>同じ拡張子のファイルをすべて無視するパターン(例: <c>*.log</c>)を作ります。</summary>
    /// <param name="relativePath">リポジトリルートからの相対パス(区切りは "/")。</param>
    /// <returns>.gitignore に書くパターン。拡張子がなければ <see langword="null"/>。</returns>
    public static string? PatternForExtension(string relativePath)
    {
        string name = relativePath.Substring(relativePath.LastIndexOf('/') + 1);
        int dot = name.LastIndexOf('.');

        // ".gitattributes" のような先頭のドットだけのファイル名や、末尾がドットの名前は拡張子なしとして扱う
        if (dot <= 0 || dot == name.Length - 1)
        {
            return null;
        }

        return "*" + Escape(name.Substring(dot));
    }

    /// <summary>リポジトリ直下の .gitignore にパターンを 1 行追加します。すでに同じ行があれば何もしません。</summary>
    /// <param name="repositoryPath">リポジトリのルートパス。</param>
    /// <param name="pattern">追加するパターン。</param>
    /// <returns>追加した場合は <see langword="true"/>、すでにあった場合は <see langword="false"/>。</returns>
    public static bool Append(string repositoryPath, string pattern)
    {
        string path = Path.Combine(repositoryPath, ".gitignore");
        string existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

        if (existing.Split('\n').Any(line => line.TrimEnd('\r') == pattern))
        {
            return false;
        }

        // 既存の改行コードに合わせ、最後の行に改行がなければ補ってから追加する
        string newLine = existing.Contains("\r\n") ? "\r\n" : "\n";
        var text = new StringBuilder();
        if (existing.Length != 0 && !existing.EndsWith("\n", StringComparison.Ordinal))
        {
            text.Append(newLine);
        }

        text.Append(pattern).Append(newLine);
        File.AppendAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return true;
    }

    /// <summary>パス中の、.gitignore で特別な意味を持つ文字をエスケープします。</summary>
    private static string Escape(string path)
    {
        var sb = new StringBuilder(path.Length + 4);
        foreach (char c in path)
        {
            if (c is '\\' or '*' or '?' or '[')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        // 行頭の "#" "!" は、呼び出し側で "/" や "*" を前に付けるので気にしなくてよい。末尾の空白は無視されるため、最後の 1 つをエスケープする
        if (sb.Length != 0 && sb[sb.Length - 1] == ' ')
        {
            sb.Insert(sb.Length - 1, '\\');
        }

        return sb.ToString();
    }
}
