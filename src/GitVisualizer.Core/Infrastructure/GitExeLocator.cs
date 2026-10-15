using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>使用する git.exe のフルパスを探します。</summary>
public sealed class GitExeLocator
{
    private const string GitExecutableName = "git.exe";

    // VS 同梱 Git の場所(<VS インストール先>\ 以下)
    private const string VsBundledGitRelativePath = @"Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd";

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
                // PATH には引用符付きや不正な文字を含むエントリがありうるため、組み立てに失敗したものは飛ばす
                var candidate = TryCombine(pathDir.Trim().Trim('"'), GitExecutableName);
                if (candidate != null)
                {
                    yield return candidate;
                }
            }
        }

        // 2. Program Files\Git を探す
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(programFiles, "Git", "cmd", GitExecutableName);

        // 32-bit Program Files (x86)
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (programFilesX86.Length != 0)
        {
            yield return Path.Combine(programFilesX86, "Git", "cmd", GitExecutableName);
        }

        // 3. VS 同梱の Git を探す(VS 2022 は "2022"、VS 2026 は "18" フォルダ)
        var vsVersions = new[] { "18", "2022" };
        var vsEditions = new[] { "Enterprise", "Professional", "Community", "Preview", "Insiders" };

        foreach (var version in vsVersions)
        {
            foreach (var edition in vsEditions)
            {
                yield return Path.Combine(
                    programFiles,
                    "Microsoft Visual Studio",
                    version,
                    edition,
                    VsBundledGitRelativePath,
                    GitExecutableName);
            }
        }
    }

    private static string? TryCombine(string directory, string fileName)
    {
        if (directory.Length == 0)
        {
            return null;
        }

        try
        {
            return Path.Combine(directory, fileName);
        }
        catch (ArgumentException)
        {
            return null;
        }
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

                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(5000))
                {
                    return false;
                }

                // 成功したか確認(exit code 0、標準出力にバージョン文字列がある)
                return process.ExitCode == 0 && output.StartsWith("git version", StringComparison.Ordinal);
            }
        }
        catch
        {
            return false;
        }
    }
}
