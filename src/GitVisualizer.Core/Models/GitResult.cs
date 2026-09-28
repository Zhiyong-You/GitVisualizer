namespace GitVisualizer.Core.Models;

/// <summary>git.exe 1 回分の実行結果。</summary>
public sealed class GitResult
{
    /// <summary><see cref="GitResult"/> を初期化します。</summary>
    /// <param name="exitCode">終了コード。</param>
    /// <param name="standardOutput">標準出力。</param>
    /// <param name="standardError">標準エラー出力。</param>
    public GitResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>終了コード。</summary>
    public int ExitCode { get; }

    /// <summary>標準出力。</summary>
    public string StandardOutput { get; }

    /// <summary>標準エラー出力。</summary>
    public string StandardError { get; }

    /// <summary>終了コードが 0 かどうか。</summary>
    public bool IsSuccess => ExitCode == 0;
}
