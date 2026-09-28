using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>git.exe を実行する抽象。テストではモックに差し替える。</summary>
public interface IGitCommandRunner
{
    /// <summary>git コマンドを 1 回実行し、終了を待って結果を返します。</summary>
    /// <param name="workingDirectory">実行ディレクトリ(リポジトリのルート)。</param>
    /// <param name="arguments">git に渡す引数(<c>git</c> 自体は含めない)。1 要素 = 1 引数。</param>
    /// <param name="environment">追加・上書きする環境変数。不要なら <see langword="null"/>。</param>
    /// <param name="progress">標準エラーに出る進捗行の通知先。不要なら <see langword="null"/>。</param>
    /// <param name="ct">キャンセル時はプロセスを終了させます。</param>
    /// <returns>終了コードと標準出力・標準エラー出力。</returns>
    Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default);
}
