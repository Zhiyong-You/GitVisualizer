using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary><see cref="System.Diagnostics.Process"/> で git.exe を起動する <see cref="IGitCommandRunner"/> 実装。</summary>
public sealed class GitProcessRunner : IGitCommandRunner
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // git をツールから非対話で使うための既定の環境変数(呼び出し側の指定が優先)
    private static readonly IReadOnlyDictionary<string, string> DefaultEnvironment = new Dictionary<string, string>
    {
        // 資格情報をターミナルで尋ねない(stdin がないため待ち続けてしまう)。GUI の資格情報マネージャーは使える
        ["GIT_TERMINAL_PROMPT"] = "0",

        // マージコミットや --continue でエディタを開かない
        ["GIT_EDITOR"] = "true",
    };

    private readonly GitExeLocator _locator;
    private readonly GitOutputParser _parser;
    private readonly object _gitPathLock = new();
    private string? _gitPath;

    /// <summary><see cref="GitProcessRunner"/> を初期化します。</summary>
    /// <param name="locator">git.exe の場所を解決するロケーター。</param>
    /// <param name="parser">進捗行を解析するパーサー。</param>
    public GitProcessRunner(GitExeLocator locator, GitOutputParser parser)
    {
        _locator = locator;
        _parser = parser;
    }

    /// <inheritdoc/>
    public Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        return RunCoreAsync(workingDirectory, arguments, environment, progress, stdout => stdout.ReadToEndAsync(), ct);
    }

    /// <inheritdoc/>
    public Task<GitResult> RunToFileAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string outputFile,
        CancellationToken ct = default)
    {
        // ファイルの中身は Shift-JIS やバイナリのこともあるため、StreamReader を通さずにバイト列のまま書き込む
        return RunCoreAsync(
            workingDirectory,
            arguments,
            environment: null,
            progress: null,
            async stdout =>
            {
                using var file = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await stdout.BaseStream.CopyToAsync(file).ConfigureAwait(false);
                return string.Empty;
            },
            ct);
    }

    private async Task<GitResult> RunCoreAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        IProgress<OperationProgress>? progress,
        Func<StreamReader, Task<string>> readStandardOutput,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // 1. git.exe のパスを解決(git --version で検証するため、結果はキャッシュする)
        string gitExePath = GetGitPath();

        // 2. ProcessStartInfo を構築(-c core.quotepath=false を付ける)
        var startInfo = new ProcessStartInfo
        {
            FileName = gitExePath,
            Arguments = BuildArguments(arguments),
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // 3. 環境変数を設定(既定値 → 呼び出し側の指定の順に上書き)
        foreach (var kvp in DefaultEnvironment)
        {
            startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        if (environment != null)
        {
            foreach (var kvp in environment)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        // 4. プロセスを起動
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("git のプロセスを起動できませんでした。");

        // git が標準入力を待たないように、すぐ閉じる
        process.StandardInput.Close();

        // stdout と stderr は並行して読む(片方のバッファが詰まるとデッドロックするため)
        Task<string> stdoutTask = readStandardOutput(process.StandardOutput);
        Task<string> stderrTask = ReadStandardErrorAsync(process.StandardError, progress);
        Task outputTask = Task.WhenAll(stdoutTask, stderrTask);

        // ct でキャンセルされたら git のプロセス(と子プロセス)を終了させる
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ct.Register(() =>
        {
            KillProcessTree(process);
            canceled.TrySetResult(true);
        }))
        {
            if (await Task.WhenAny(outputTask, canceled.Task).ConfigureAwait(false) != outputTask)
            {
                throw new OperationCanceledException(ct);
            }
        }

        // 出力の読み取りが終わった時点で、プロセスはほぼ終了している
        await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);

        return new GitResult(process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private string GetGitPath()
    {
        lock (_gitPathLock)
        {
            if (_gitPath == null || !File.Exists(_gitPath))
            {
                _gitPath = _locator.FindGitExecutable()
                    ?? throw new InvalidOperationException("git.exe が見つかりません。Git をインストールし、PATH を通してください。");
            }

            return _gitPath;
        }
    }

    /// <summary>git コマンドの引数を構築します(-c core.quotepath=false を付ける)。</summary>
    internal static string BuildArguments(IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder("-c core.quotepath=false");

        foreach (var arg in arguments)
        {
            sb.Append(' ');
            AppendQuotedArgument(sb, arg);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Windows の標準的なコマンドライン解析規則(CommandLineToArgvW)で 1 引数として解釈されるように書き込みます。
    /// </summary>
    private static void AppendQuotedArgument(StringBuilder sb, string arg)
    {
        if (arg.Length != 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
        {
            sb.Append(arg);
            return;
        }

        sb.Append('"');
        int backslashes = 0;
        foreach (char ch in arg)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                // 引用符の直前のバックスラッシュは 2 倍にし、引用符自体もエスケープする
                sb.Append('\\', (backslashes * 2) + 1);
            }
            else
            {
                sb.Append('\\', backslashes);
            }

            backslashes = 0;
            sb.Append(ch);
        }

        // 閉じ引用符の直前のバックスラッシュは 2 倍にする
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
    }

    /// <summary>標準エラーをすべて読み取りながら、\r / \n 区切りの進捗行を通知します。</summary>
    private async Task<string> ReadStandardErrorAsync(StreamReader reader, IProgress<OperationProgress>? progress)
    {
        var all = new StringBuilder();
        var line = new StringBuilder();
        char[] buffer = new char[4096];
        int read;

        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            all.Append(buffer, 0, read);
            if (progress == null)
            {
                continue;
            }

            for (int i = 0; i < read; i++)
            {
                char ch = buffer[i];
                if (ch == '\r' || ch == '\n')
                {
                    // git は進捗を \r で上書きしながら出すので、\r でも 1 行として扱う
                    if (line.Length != 0 && _parser.TryParseProgress(line.ToString(), out OperationProgress? parsed) && parsed != null)
                    {
                        progress.Report(parsed);
                    }

                    line.Clear();
                }
                else
                {
                    line.Append(ch);
                }
            }
        }

        return all.ToString();
    }

    /// <summary>プロセスとその子プロセスを終了させます(Program Files\Git\cmd\git.exe は本体を子プロセスとして起動するため)。</summary>
    private static void KillProcessTree(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            using var taskkill = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = $"/T /F /PID {process.Id}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            taskkill?.WaitForExit(5000);
        }
        catch
        {
            // taskkill が使えない場合は本体だけでも終了させる
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
            // 既に終了している
        }
    }
}
