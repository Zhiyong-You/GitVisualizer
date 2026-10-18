using System;
using System.IO;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>差分表示用の一時ファイルを置くフォルダ(%TEMP%\GitVisualizer\diff)。</summary>
public static class DiffTempDirectory
{
    /// <summary>一時ファイルのルートフォルダ。</summary>
    public static string RootPath { get; } = Path.Combine(Path.GetTempPath(), "GitVisualizer", "diff");

    /// <summary>差分 1 回分の空のフォルダを作成します。</summary>
    /// <returns>作成したフォルダのフルパス。</returns>
    public static string CreateSessionDirectory()
    {
        string path = Path.Combine(RootPath, Guid.NewGuid().ToString("N").Substring(0, 12));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 指定した時間より前に作られたフォルダを削除します。
    /// 差分ウィンドウが開いている間はファイルを消さないよう、作成直後のものは残します。
    /// </summary>
    /// <param name="olderThan">これより古いフォルダを削除する。</param>
    public static void CleanUp(TimeSpan olderThan)
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        DateTime threshold = DateTime.UtcNow - olderThan;
        foreach (string directory in Directory.GetDirectories(RootPath))
        {
            try
            {
                if (Directory.GetCreationTimeUtc(directory) >= threshold)
                {
                    continue;
                }

                // 一時ファイルは読み取り専用にしているため、属性を外してから消す
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // 別の VS で開いているなどで消せなければ、次回に回す
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
