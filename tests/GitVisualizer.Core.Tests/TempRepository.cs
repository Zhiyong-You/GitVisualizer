using System;
using System.IO;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Tests;

/// <summary>結合テスト用に一時フォルダへ作る git リポジトリ。破棄時に削除する。</summary>
internal sealed class TempRepository : IDisposable
{
    private TempRepository(string path, GitProcessRunner runner)
    {
        Path = path;
        Runner = runner;
    }

    public string Path { get; }

    public GitProcessRunner Runner { get; }

    public GitOutputParser Parser { get; } = new();

    public static async Task<TempRepository> CreateAsync()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GitVisualizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        var repo = new TempRepository(path, new GitProcessRunner(new GitExeLocator(), new GitOutputParser()));
        await repo.GitAsync("init", "-q", "-b", "main");

        // 利用者のグローバル設定(署名やフックなど)の影響を受けないようにする
        await repo.GitAsync("config", "user.name", "Test User");
        await repo.GitAsync("config", "user.email", "test@example.com");
        await repo.GitAsync("config", "commit.gpgsign", "false");
        await repo.GitAsync("config", "core.autocrlf", "false");
        return repo;
    }

    /// <summary>git を実行し、失敗したら例外にする。</summary>
    public async Task<GitResult> GitAsync(params string[] args)
    {
        GitResult result = await Runner.RunAsync(Path, args);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"git {string.Join(" ", args)} failed: {result.StandardError}");
        }

        return result;
    }

    public void WriteFile(string relativePath, string content)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public async Task<string> CommitFileAsync(string relativePath, string content, string message)
    {
        WriteFile(relativePath, content);
        await GitAsync("add", "--", relativePath);
        await GitAsync("commit", "-q", "-m", message);
        return (await GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();
    }

    public void Dispose()
    {
        try
        {
            // git のオブジェクトは読み取り専用属性が付くため外してから消す
            foreach (string file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
