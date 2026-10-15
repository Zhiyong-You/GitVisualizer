using System;
using System.IO;
using System.Text;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>コミットメッセージを渡すための一時ファイル(UTF-8、BOM なし)。破棄時に削除します。</summary>
internal sealed class TempMessageFile : IDisposable
{
    /// <summary>メッセージを書き込んだ一時ファイルを作成します。</summary>
    /// <param name="message">コミットメッセージ。</param>
    public TempMessageFile(string message)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"GitVisualizer_{Guid.NewGuid():N}.txt");

        // git に CRLF が混ざらないよう LF に揃える
        string normalized = message.Replace("\r\n", "\n").Replace('\r', '\n');
        File.WriteAllText(Path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>一時ファイルのフルパス。</summary>
    public string Path { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // 削除できなくても一時フォルダなので問題ない
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
