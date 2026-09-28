using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary><see cref="System.Diagnostics.Process"/> で git.exe を起動する <see cref="IGitCommandRunner"/> 実装。</summary>
public sealed class GitProcessRunner : IGitCommandRunner
{
    private readonly GitExeLocator _locator;

    /// <summary><see cref="GitProcessRunner"/> を初期化します。</summary>
    /// <param name="locator">git.exe の場所を解決するロケーター。</param>
    public GitProcessRunner(GitExeLocator locator)
    {
        _locator = locator;
    }

    /// <inheritdoc/>
    public Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        // TODO: git -c core.quotepath=false <arguments> を UTF-8 リダイレクトで起動し、stderr の進捗行を progress に流す
        throw new NotImplementedException();
    }
}
