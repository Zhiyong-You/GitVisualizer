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
    private readonly GitExeLocator _locator;
    private readonly GitOutputParser _parser;

    /// <summary><see cref="GitProcessRunner"/> を初期化します。</summary>
    /// <param name="locator">git.exe の場所を解決するロケーター。</param>
    /// <param name="parser">進捗行を解析するパーサー。</param>
    public GitProcessRunner(GitExeLocator locator, GitOutputParser parser)
    {
        _locator = locator;
        _parser = parser;
    }

    /// <inheritdoc/>
    public async Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        // 1. git.exe のパスを解決
        string? gitExePath = _locator.FindGitExecutable();
        if (gitExePath == null || !File.Exists(gitExePath))
        {
            throw new InvalidOperationException("git.exe が見つかりません。PATH に Git をインストールしてください。");
        }

        // 2. ProcessStartInfo を構築（-c core.quotepath=false を付ける）
        var startInfo = new ProcessStartInfo
        {
            FileName = gitExePath,
            Arguments = BuildArguments(arguments),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // 3. 環境変数を設定(あれば)
        if (environment != null)
        {
            foreach (var kvp in environment)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        // 4. プロセスを起動
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("プロセスの起動に失敗しました。");

        try
        {
            // stdout と stderr を UTF-8 でリダイレクト（非同期読み込みでデッドロック回避）
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // stderr の \r 区切りの進捗行を TryParseProgress で progress に流す
            if (progress != null)
            {
                _ = MonitorProgressAsync(process.StandardError, progress, ct);
            }

            // ct でキャンセルされたら git のプロセスを Kill する
            using (ct.Register(process.Kill))
            {
                // プロセス終了を待機
                await Task.Run(process.WaitForExit, ct);

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                // 結果を返す
                return new GitResult(process.ExitCode, stdout, stderr);
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>git コマンドの引数を構築します（-c core.quotepath=false を付ける）。</summary>
    private static string BuildArguments(IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder();
        sb.Append("-c core.quotepath=false");

        foreach (var arg in arguments)
        {
            sb.Append(" ");
            // 引数にスペースが含まれる場合はクォートで囲む
            if (arg.Contains(" "))
            {
                sb.Append("\"").Append(arg).Append("\"");
            }
            else
            {
                sb.Append(arg);
            }
        }

        return sb.ToString();
    }

    /// <summary>stderr の進捗行を監視します（\r 区切りの行を処理）。</summary>
    private async Task MonitorProgressAsync(
        TextReader stderrReader,
        IProgress<OperationProgress> progress,
        CancellationToken ct)
    {
        var buffer = new StringBuilder();
        char[] charBuffer = new char[1024];
        int charsRead;

        try
        {
            while ((charsRead = await stderrReader.ReadAsync(charBuffer, 0, charBuffer.Length)) > 0)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                for (int i = 0; i < charsRead; i++)
                {
                    char ch = charBuffer[i];

                    if (ch == '\n')
                    {
                        // \n で行が終わったとき（\r\n の \n、または単独の \n）
                        string line = buffer.ToString();
                        if (line.Length > 0)
                        {
                            // TryParseProgress は \r 区切りを内部で処理する
                            if (_parser.TryParseProgress(line, out var progressInfo))
                            {
                                progress.Report(progressInfo);
                            }
                        }
                        buffer.Clear();
                    }
                    else if (ch != '\r')
                    {
                        // \r 以外の文字をバッファに蓄積
                        buffer.Append(ch);
                    }
                }
            }
        }
        catch
        {
            // 読み込み中のキャンセルなど、エラーは無視
        }
    }
}
