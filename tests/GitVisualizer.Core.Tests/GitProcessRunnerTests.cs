using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class GitProcessRunnerTests
{
    [Fact]
    public void BuildArguments_空白や引用符を含む引数を1引数としてクォートする()
    {
        string args = GitProcessRunner.BuildArguments(new[] { "commit", "-m", "a \"b\" c", "", @"C:\dir with space\" });

        Assert.Equal(@"-c core.quotepath=false commit -m ""a \""b\"" c"" """" ""C:\dir with space\\""", args);
    }

    [Fact]
    public async Task RunAsync_日本語のファイル名とメッセージをUTF8で扱える()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("日本語.txt", "内容", "日本語のメッセージ");

        GitResult log = await repo.GitAsync("log", "-1", "--format=%s");
        GitResult files = await repo.GitAsync("ls-files");

        Assert.Equal("日本語のメッセージ", log.StandardOutput.Trim());
        Assert.Equal("日本語.txt", files.StandardOutput.Trim());
    }

    [Fact]
    public async Task RunAsync_失敗時は終了コードと標準エラーを返す()
    {
        using var repo = await TempRepository.CreateAsync();

        GitResult result = await repo.Runner.RunAsync(repo.Path, new[] { "rev-parse", "--verify", "no-such-ref" });

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.StandardError);
    }

    [Fact]
    public async Task RunAsync_キャンセルするとOperationCanceledException()
    {
        using var repo = await TempRepository.CreateAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repo.Runner.RunAsync(repo.Path, new[] { "status" }, ct: cts.Token));
    }

    [Fact]
    public async Task RunAsync_環境変数を渡せる()
    {
        using var repo = await TempRepository.CreateAsync();
        var env = new Dictionary<string, string> { ["GIT_AUTHOR_NAME"] = "Env Author" };

        await repo.Runner.RunAsync(repo.Path, new[] { "commit", "--allow-empty", "-q", "-m", "x" }, env);
        GitResult log = await repo.GitAsync("log", "-1", "--format=%an");

        Assert.Equal("Env Author", log.StandardOutput.Trim());
    }
}
