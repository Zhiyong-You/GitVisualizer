using System;
using System.IO;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>diff の出力や <c>git apply</c> に渡すパッチを置く一時ファイル。破棄時に削除します。</summary>
internal sealed class TempPatchFile : IDisposable
{
    /// <summary>一時ファイルを作成します。</summary>
    /// <param name="content">書き込む内容。空のファイルを作る場合は <see langword="null"/>。</param>
    public TempPatchFile(byte[]? content = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"GitVisualizer_{Guid.NewGuid():N}.patch");

        // 文字コード・改行コードを変えないよう、バイト列のまま書き込む
        File.WriteAllBytes(Path, content ?? Array.Empty<byte>());
    }

    /// <summary>一時ファイルのフルパス。</summary>
    public string Path { get; }

    /// <summary>一時ファイルの内容を読み取ります。</summary>
    /// <returns>ファイルの内容。</returns>
    public byte[] ReadAllBytes() => File.ReadAllBytes(Path);

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
