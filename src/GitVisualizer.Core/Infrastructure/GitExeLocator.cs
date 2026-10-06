using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>使用する git.exe のフルパスを探します。</summary>
public sealed class GitExeLocator
{
    private const string GitExecutableName = "git.exe";

    /// <summary>git.exe のフルパスを返します。</summary>
    /// <returns>見つかった git.exe のフルパス。見つからない場合は <see langword="null"/>。</returns>
    public string? FindGitExecutable()
    {
        // git.exe の候補パスを列挙
        var candidates = GetGitCandidates();

        // 各候補をテスト
        foreach (var candidate in candidates)
        {
            if (VerifyGitExecutable(candidate))
            {
                return candidate;
            }
        }

        // すべての候補が失敗した場合は null を返す
        return null;
    }

    /// <summary>git.exe の候補パスを列挙します。</summary>
    private static IEnumerable<string> GetGitCandidates()
    {
        // 1. PATH 環境変数内を探す
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var pathDir in pathEnv.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(pathDir, GitExecutableName);
                yield return candidate;
            }
        }

        // 2. VS 同梱の Git を探す
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var vsVersions = new[] { "2022", "2026" };
        var vsEditions = new[] { "Enterprise", "Professional", "Community" };

        foreach (var version in vsVersions)
        {
            foreach (var edition in vsEditions)
            {
                var vsGitPath = Path.Combine(
                    programFiles,
                    "Microsoft Visual Studio",
                    version,
                    edition,
                    "Common7\\IDE\\CommonExtensions\\Microsoft\\Git\\cmd",
                    GitExecutableName);
                yield return vsGitPath;
            }
        }

        // 3. Program Files\Git を探す
        var gitProgramFiles = Path.Combine(programFiles, "Git", "cmd", GitExecutableName);
        yield return gitProgramFiles;

        // 32-bit Program Files (x86)
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var gitProgramFilesX86 = Path.Combine(programFilesX86, "Git", "cmd", GitExecutableName);
        yield return gitProgramFilesX86;
    }

    /// <summary>git.exe が動作するか確認します。</summary>
    private static bool VerifyGitExecutable(string gitPath)
    {
        try
        {
            // ファイルが存在するか確認
            if (!File.Exists(gitPath))
            {
                return false;
            }

            // "git --version" を実行
            var startInfo = new ProcessStartInfo
            {
                FileName = gitPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit(5000); // 5 seconds in milliseconds

                // 成功したか確認(exit code 0、標準出力にデータがある)
                return process.ExitCode == 0 && process.StandardOutput.Peek() >= 0;
            }
        }
        catch
        {
            return false;
        }
    }
}
