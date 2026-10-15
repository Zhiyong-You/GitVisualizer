using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using Xunit;

namespace GitVisualizer.Core.Tests;

/// <summary>一時リポジトリで実際に git を実行するサービスの結合テスト。</summary>
public class ServiceIntegrationTests
{
    private static string EditorHelperPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GitEditorHelper.exe");

    private static HistoryService CreateHistoryService(TempRepository repo) =>
        new(repo.Runner, repo.Parser, new RepositoryStateService(repo.Runner), EditorHelperPath);

    [Fact]
    public async Task CommitService_ステージ_コミット_アンステージ()
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        repo.WriteFile("a.txt", "a");

        var status = await service.GetStatusAsync(repo.Path);
        Assert.Equal(FileChangeKind.Untracked, Assert.Single(status).Kind);

        // コミットがまだないリポジトリでもステージを外せる
        Assert.True((await service.StageAsync(repo.Path, new[] { "a.txt" })).IsSuccess);
        Assert.True((await service.UnstageAsync(repo.Path, new[] { "a.txt" })).IsSuccess);
        Assert.False(Assert.Single(await service.GetStatusAsync(repo.Path)).IsStaged);

        await service.StageAsync(repo.Path, new[] { "a.txt" });
        var staged = Assert.Single(await service.GetStatusAsync(repo.Path));
        Assert.True(staged.IsStaged);
        Assert.Equal(FileChangeKind.Added, staged.Kind);

        GitResult commit = await service.CommitAsync(repo.Path, "1 行目\r\n\r\n本文", amend: false);
        Assert.True(commit.IsSuccess, commit.StandardError);
        Assert.Empty(await service.GetStatusAsync(repo.Path));
        Assert.Equal("1 行目\n\n本文", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());

        repo.WriteFile("a.txt", "changed");
        await service.StageAsync(repo.Path, new[] { "a.txt" });
        await service.UnstageAsync(repo.Path, new[] { "a.txt" });
        var unstaged = Assert.Single(await service.GetStatusAsync(repo.Path));
        Assert.False(unstaged.IsStaged);
        Assert.Equal(FileChangeKind.Modified, unstaged.Kind);

        GitResult amend = await service.CommitAsync(repo.Path, "修正後", amend: true);
        Assert.True(amend.IsSuccess, amend.StandardError);
        Assert.Equal("1", (await repo.GitAsync("rev-list", "--count", "HEAD")).StandardOutput.Trim());
    }

    [Fact]
    public async Task BranchService_一覧取得と未コミット変更を退避したチェックアウト()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("a.txt", "base", "base");
        await repo.GitAsync("branch", "feature");
        var service = new BranchService(repo.Runner, repo.Parser);

        var branches = await service.GetBranchesAsync(repo.Path);
        Assert.Equal(new[] { "feature", "main" }, branches.Select(b => b.Name).OrderBy(n => n));
        Assert.True(branches.Single(b => b.Name == "main").IsCurrent);

        repo.WriteFile("a.txt", "dirty");
        GitResult result = await service.CheckoutAsync(
            repo.Path,
            branches.Single(b => b.Name == "feature"),
            new CheckoutOptions { Handling = ChangeHandling.Stash });

        Assert.True(result.IsSuccess, result.StandardError);
        Assert.Equal("feature", (await repo.GitAsync("branch", "--show-current")).StandardOutput.Trim());
        Assert.Equal("dirty", File.ReadAllText(Path.Combine(repo.Path, "a.txt")));
        Assert.Equal(string.Empty, (await repo.GitAsync("stash", "list")).StandardOutput.Trim());
    }

    [Fact]
    public async Task BranchService_変更を破棄してチェックアウト()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("a.txt", "base", "base");
        await repo.GitAsync("switch", "-q", "-c", "feature");
        await repo.CommitFileAsync("a.txt", "feature", "feature");
        repo.WriteFile("a.txt", "dirty");
        var service = new BranchService(repo.Runner, repo.Parser);
        var main = (await service.GetBranchesAsync(repo.Path)).Single(b => b.Name == "main");

        GitResult keep = await service.CheckoutAsync(repo.Path, main, new CheckoutOptions { Handling = ChangeHandling.Keep });
        Assert.False(keep.IsSuccess);

        GitResult discard = await service.CheckoutAsync(repo.Path, main, new CheckoutOptions { Handling = ChangeHandling.Discard });
        Assert.True(discard.IsSuccess, discard.StandardError);
        Assert.Equal("base", File.ReadAllText(Path.Combine(repo.Path, "a.txt")));
    }

    [Fact]
    public async Task HistoryService_ログと変更ファイルを取得する()
    {
        using var repo = await TempRepository.CreateAsync();
        var history = CreateHistoryService(repo);
        Assert.Empty(await history.GetLogAsync(repo.Path, 100));

        string first = await repo.CommitFileAsync("a.txt", "a", "first");
        await repo.GitAsync("mv", "a.txt", "b.txt");
        await repo.GitAsync("commit", "-q", "-m", "rename");

        var log = await history.GetLogAsync(repo.Path, 100);
        Assert.Equal(new[] { "rename", "first" }, log.Select(c => c.Subject));
        Assert.Contains("HEAD -> main", log[0].Refs);

        var changes = await history.GetCommitChangesAsync(repo.Path, log[0].Sha);
        var renamed = Assert.Single(changes);
        Assert.Equal(FileChangeKind.Renamed, renamed.Kind);
        Assert.Equal("a.txt", renamed.OldPath);
        Assert.Equal(FileChangeKind.Added, Assert.Single(await history.GetCommitChangesAsync(repo.Path, first)).Kind);
    }

    [Fact]
    public async Task HistoryService_HEADのコミットメッセージを変更する()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a", "old");
        string head = await repo.CommitFileAsync("b.txt", "b", "old head");
        repo.WriteFile("staged.txt", "s");
        await repo.GitAsync("add", "staged.txt");

        GitResult result = await CreateHistoryService(repo).RewordAsync(repo.Path, head, "new head");

        Assert.True(result.IsSuccess, result.StandardError);
        Assert.Equal("new head", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());

        // ステージ済みの変更はコミットに含まれない
        Assert.Contains("staged.txt", (await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task HistoryService_過去のコミットメッセージをGitEditorHelperで変更する(int index)
    {
        Assert.True(File.Exists(EditorHelperPath), "GitEditorHelper.exe がテストの出力フォルダにありません。");

        using var repo = await TempRepository.CreateAsync();
        string[] shas =
        {
            await repo.CommitFileAsync("a.txt", "a", "root"),
            await repo.CommitFileAsync("b.txt", "b", "second"),
            await repo.CommitFileAsync("c.txt", "c", "third"),
        };
        repo.WriteFile("c.txt", "dirty");

        GitResult result = await CreateHistoryService(repo).RewordAsync(repo.Path, shas[index], "書き換え後\n\n本文");

        Assert.True(result.IsSuccess, result.StandardError);
        string subjects = (await repo.GitAsync("log", "--format=%s", "--reverse")).StandardOutput;
        string[] expected = { "root", "second", "third" };
        expected[index] = "書き換え後";
        Assert.Equal(expected, subjects.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));

        // --autostash で未コミット変更は戻る
        Assert.Equal("dirty", File.ReadAllText(Path.Combine(repo.Path, "c.txt")));
    }

    [Fact]
    public async Task HistoryService_チェリーピックの競合を検出して中止できる()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("a.txt", "base", "base");
        await repo.GitAsync("switch", "-q", "-c", "feature");
        string pick = await repo.CommitFileAsync("a.txt", "feature", "feature change");
        await repo.GitAsync("switch", "-q", "main");
        await repo.CommitFileAsync("a.txt", "main", "main change");

        var state = new RepositoryStateService(repo.Runner);
        var history = new HistoryService(repo.Runner, repo.Parser, state, EditorHelperPath);

        GitResult result = await history.CherryPickAsync(repo.Path, new[] { pick }, new CherryPickOptions { RecordOrigin = true });
        Assert.False(result.IsSuccess);
        Assert.Equal(RepositoryOperation.CherryPick, await state.GetOperationAsync(repo.Path));

        var status = await new CommitService(repo.Runner, repo.Parser).GetStatusAsync(repo.Path);
        Assert.Equal(FileChangeKind.Conflicted, Assert.Single(status).Kind);

        GitResult abort = await history.AbortAsync(repo.Path);
        Assert.True(abort.IsSuccess, abort.StandardError);
        Assert.Equal(RepositoryOperation.None, await state.GetOperationAsync(repo.Path));
    }

    [Fact]
    public async Task HistoryService_リベースの競合を解決して続行できる()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("a.txt", "base", "base");
        await repo.GitAsync("switch", "-q", "-c", "feature");
        await repo.CommitFileAsync("a.txt", "feature", "feature change");
        await repo.GitAsync("switch", "-q", "main");
        await repo.CommitFileAsync("a.txt", "main", "main change");
        await repo.GitAsync("switch", "-q", "feature");

        var state = new RepositoryStateService(repo.Runner);
        var history = new HistoryService(repo.Runner, repo.Parser, state, EditorHelperPath);

        GitResult rebase = await history.RebaseAsync(repo.Path, new RebaseOptions { Onto = "main" });
        Assert.False(rebase.IsSuccess);
        Assert.Equal(RepositoryOperation.Rebase, await state.GetOperationAsync(repo.Path));

        // 競合を解決してステージし、続行する(エディタは開かない)
        repo.WriteFile("a.txt", "resolved");
        await repo.GitAsync("add", "a.txt");
        GitResult cont = await history.ContinueAsync(repo.Path);

        Assert.True(cont.IsSuccess, cont.StandardError);
        Assert.Equal(RepositoryOperation.None, await state.GetOperationAsync(repo.Path));
        Assert.Equal("feature change\nmain change\nbase", (await repo.GitAsync("log", "--format=%s")).StandardOutput.Trim().Replace("\r", string.Empty));
    }

    [Fact]
    public async Task SyncService_ローカルのリモートへpushしてfetchとpullできる()
    {
        using var remote = await TempRepository.CreateAsync();
        await remote.GitAsync("config", "receive.denyCurrentBranch", "updateInstead");
        await remote.CommitFileAsync("a.txt", "a", "initial");

        using var local = await TempRepository.CreateAsync();
        await local.GitAsync("remote", "add", "origin", remote.Path);
        var sync = new SyncService(local.Runner);

        GitResult fetch = await sync.FetchAsync(local.Path, null);
        Assert.True(fetch.IsSuccess, fetch.StandardError);
        await local.GitAsync("switch", "-q", "--track", "origin/main");

        await local.CommitFileAsync("b.txt", "b", "from local");
        GitResult push = await sync.PushAsync(local.Path, new PushOptions { SetUpstream = true });
        Assert.True(push.IsSuccess, push.StandardError);
        Assert.Equal("from local", (await remote.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());

        await remote.CommitFileAsync("c.txt", "c", "from remote");
        GitResult pull = await sync.PullAsync(local.Path, new PullOptions { Mode = PullMode.FastForwardOnly });
        Assert.True(pull.IsSuccess, pull.StandardError);
        Assert.Equal("from remote", (await local.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    [Fact]
    public async Task RepositoryStateService_ファイル変更でChangedが発生する()
    {
        using var repo = await TempRepository.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a", "first");
        using var state = new RepositoryStateService(repo.Runner);
        using var changed = new SemaphoreSlim(0);
        state.Changed += (_, _) => changed.Release();

        state.Watch(repo.Path);
        repo.WriteFile("a.txt", "changed");

        Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(10)), "Changed が発生しませんでした。");
    }
}
