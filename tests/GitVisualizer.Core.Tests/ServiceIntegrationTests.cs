using System;
using System.IO;
using System.Linq;
using System.Text;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitService_差分はステージ済みと未ステージを区別してバイト列を保持する(bool staged)
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        await repo.CommitFileAsync("dir/日本語.dat", "base", "init");
        byte[] before = { 0x82, 0xA0, 0x00, 0xFF };
        byte[] index = { 0x82, 0xA2, 0x00, 0xFE };
        byte[] working = { 0x82, 0xA4, 0x00, 0xFD };
        string fullPath = Path.Combine(repo.Path, "dir/日本語.dat");
        File.WriteAllBytes(fullPath, before);
        await repo.GitAsync("add", "-A");
        await repo.GitAsync("commit", "-q", "-m", "binary");
        File.WriteAllBytes(fullPath, index);
        await repo.GitAsync("add", "-A");
        File.WriteAllBytes(fullPath, working);

        FileChange change = (await service.GetStatusAsync(repo.Path)).Single(c => c.IsStaged == staged);
        FileDiff diff = await service.GetFileDiffAsync(repo.Path, change);
        try
        {
            Assert.Equal(staged ? before : index, File.ReadAllBytes(diff.LeftFile));
            Assert.Equal(staged ? index : working, File.ReadAllBytes(diff.RightFile));
            Assert.Equal("日本語.dat", Path.GetFileName(diff.RightFile));
            Assert.Equal(working, File.ReadAllBytes(fullPath));
        }
        finally
        {
            DeleteDiffFiles(diff);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CommitService_追加と削除の差分は存在しない側を空にする(bool staged, bool deleted)
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        if (deleted)
        {
            await repo.CommitFileAsync("file.txt", "content", "init");
            File.Delete(Path.Combine(repo.Path, "file.txt"));
        }
        else
        {
            repo.WriteFile("file.txt", "content");
        }

        if (staged)
        {
            await repo.GitAsync("add", "-A");
        }

        FileDiff diff = await service.GetFileDiffAsync(repo.Path, Assert.Single(await service.GetStatusAsync(repo.Path)));
        try
        {
            Assert.Equal(deleted ? "content" : string.Empty, File.ReadAllText(diff.LeftFile));
            Assert.Equal(deleted ? string.Empty : "content", File.ReadAllText(diff.RightFile));
        }
        finally
        {
            DeleteDiffFiles(diff);
        }
    }

    [Fact]
    public async Task CommitService_名前変更の差分は変更前のパスとインデックスを使う()
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        await repo.CommitFileAsync("old.txt", "original", "init");
        await repo.GitAsync("mv", "old.txt", "new.txt");
        repo.WriteFile("new.txt", "working");
        FileChange change = (await service.GetStatusAsync(repo.Path)).Single(c => c.IsStaged);
        Assert.Equal(FileChangeKind.Renamed, change.Kind);

        FileDiff diff = await service.GetFileDiffAsync(repo.Path, change);
        try
        {
            Assert.Equal("original", File.ReadAllText(diff.LeftFile));
            Assert.Equal("original", File.ReadAllText(diff.RightFile));
            Assert.Equal("old.txt", Path.GetFileName(diff.LeftFile));
            Assert.Equal("new.txt", Path.GetFileName(diff.RightFile));
        }
        finally
        {
            DeleteDiffFiles(diff);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitService_競合中はHEADと作業ツリーの差分を開く(bool deletedByUs)
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        await repo.CommitFileAsync("file.txt", "base", "init");
        await repo.GitAsync("switch", "-q", "-c", "feature");
        await repo.CommitFileAsync("file.txt", "feature", "feature");
        await repo.GitAsync("switch", "-q", "main");
        if (deletedByUs)
        {
            await repo.GitAsync("rm", "file.txt");
            await repo.GitAsync("commit", "-q", "-m", "delete");
        }
        else
        {
            await repo.CommitFileAsync("file.txt", "main", "main");
        }

        GitResult merge = await repo.Runner.RunAsync(repo.Path, new[] { "merge", "feature" });
        Assert.False(merge.IsSuccess);
        FileChange change = Assert.Single(await service.GetStatusAsync(repo.Path));
        Assert.Equal(FileChangeKind.Conflicted, change.Kind);
        FileDiff diff = await service.GetFileDiffAsync(repo.Path, change);
        try
        {
            Assert.Equal(deletedByUs ? string.Empty : "main", File.ReadAllText(diff.LeftFile));
            Assert.Equal(File.ReadAllBytes(Path.Combine(repo.Path, "file.txt")), File.ReadAllBytes(diff.RightFile));
        }
        finally
        {
            DeleteDiffFiles(diff);
        }
    }

    [Fact]
    public async Task CommitService_行単位のステージとステージ解除()
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        await repo.CommitFileAsync("f.txt", "a\nb\nc\nd\ne\n", "init");
        repo.WriteFile("f.txt", "a\nB1\nB2\nc\nd\nE\n");

        // "-b" と "+B1" だけをステージ
        FileChange unstaged = Assert.Single(await service.GetStatusAsync(repo.Path));
        FilePatch patch = await service.GetFilePatchAsync(repo.Path, unstaged);
        GitResult stage = await service.StageLinesAsync(repo.Path, patch, patch.ChangeLines.Where(l => l.Text is "b" or "B1").ToList());
        Assert.True(stage.IsSuccess, stage.StandardError);
        Assert.Equal("a\nB1\nc\nd\ne\n", (await repo.GitAsync("show", ":f.txt")).StandardOutput);

        // ステージ済みの側から "-b" だけを解除
        FileChange staged = (await service.GetStatusAsync(repo.Path)).Single(c => c.IsStaged);
        FilePatch stagedPatch = await service.GetFilePatchAsync(repo.Path, staged);
        GitResult unstage = await service.UnstageLinesAsync(repo.Path, stagedPatch, stagedPatch.ChangeLines.Where(l => l.Text == "b").ToList());
        Assert.True(unstage.IsSuccess, unstage.StandardError);
        Assert.Equal("a\nb\nB1\nc\nd\ne\n", (await repo.GitAsync("show", ":f.txt")).StandardOutput);

        // 作業ツリーは変わらない
        Assert.Equal("a\nB1\nB2\nc\nd\nE\n", File.ReadAllText(Path.Combine(repo.Path, "f.txt")));
    }

    [Fact]
    public async Task CommitService_未追跡ファイルの一部の行をステージ()
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        await repo.CommitFileAsync("keep.txt", "k\n", "init");
        repo.WriteFile("n.txt", "x\ny\nz\n");

        FileChange untracked = Assert.Single(await service.GetStatusAsync(repo.Path));
        FilePatch patch = await service.GetFilePatchAsync(repo.Path, untracked);
        Assert.True(patch.IsNewFile);

        GitResult result = await service.StageLinesAsync(repo.Path, patch, patch.ChangeLines.Where(l => l.Text != "y").ToList());
        Assert.True(result.IsSuccess, result.StandardError);
        Assert.Equal("x\nz\n", (await repo.GitAsync("show", ":n.txt")).StandardOutput);

        var status = await service.GetStatusAsync(repo.Path);
        Assert.Contains(status, c => c.Path == "n.txt" && c.IsStaged && c.Kind == FileChangeKind.Added);
        Assert.Contains(status, c => c.Path == "n.txt" && !c.IsStaged);
    }

    [Fact]
    public async Task CommitService_ShiftJISとCRLFのファイルを行単位でステージ()
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        Encoding sjis = Encoding.GetEncoding(932);
        string full = Path.Combine(repo.Path, "s.txt");
        File.WriteAllBytes(full, sjis.GetBytes("あ\r\nい\r\nう\r\n"));
        await repo.GitAsync("add", "--", "s.txt");
        await repo.GitAsync("commit", "-q", "-m", "init");
        File.WriteAllBytes(full, sjis.GetBytes("あ\r\nイ\r\nう\r\nえ\r\n"));

        FilePatch patch = await service.GetFilePatchAsync(repo.Path, Assert.Single(await service.GetStatusAsync(repo.Path)));
        Assert.Equal(new[] { "い", "イ", "え" }, patch.ChangeLines.Select(l => l.Text));

        GitResult result = await service.StageLinesAsync(repo.Path, patch, patch.ChangeLines.Where(l => l.Text == "え").ToList());
        Assert.True(result.IsSuccess, result.StandardError);

        string indexFile = Path.Combine(repo.Path, "index.out");
        await repo.Runner.RunToFileAsync(repo.Path, new[] { "show", ":s.txt" }, indexFile);
        Assert.Equal(sjis.GetBytes("あ\r\nい\r\nう\r\nえ\r\n"), File.ReadAllBytes(indexFile));
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
    public async Task HistoryService_チェックアウトせずにブランチの履歴を取得する()
    {
        using var repo = await TempRepository.CreateAsync();
        var history = CreateHistoryService(repo);
        await repo.CommitFileAsync("a.txt", "a", "base");
        await repo.GitAsync("checkout", "-q", "-b", "feature");
        await repo.CommitFileAsync("f.txt", "f", "on feature");
        await repo.GitAsync("checkout", "-q", "main");
        await repo.CommitFileAsync("m.txt", "m", "on main");

        var feature = await history.GetLogAsync(repo.Path, 100, new[] { "refs/heads/feature" });
        Assert.Equal(new[] { "on feature", "base" }, feature.Select(c => c.Subject));

        var head = await history.GetLogAsync(repo.Path, 100, new[] { "HEAD" });
        Assert.Equal(new[] { "on main", "base" }, head.Select(c => c.Subject));

        // 現在のブランチと選択ブランチを合わせて表示する。存在しない起点は無視する
        var both = await history.GetLogAsync(repo.Path, 100, new[] { "HEAD", "refs/heads/feature", "refs/heads/deleted" });
        Assert.Equal(new[] { "base", "on feature", "on main" }, both.Select(c => c.Subject).OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal("base", both[both.Count - 1].Subject);

        Assert.Equal(3, (await history.GetLogAsync(repo.Path, 100)).Count);
        Assert.Empty(await history.GetLogAsync(repo.Path, 100, new[] { "refs/heads/deleted" }));
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

    [Fact]
    public async Task HistoryService_コミットの変更ファイルの差分を取り出せる()
    {
        using var repo = await TempRepository.CreateAsync();
        var service = CreateHistoryService(repo);

        // Shift-JIS(「あ」)と NUL を含むバイト列が、文字コード変換されずにそのまま取り出せること
        byte[] binaryBefore = { 0x82, 0xA0, 0x00, 0xFF };
        byte[] binaryAfter = { 0x82, 0xA2, 0x00, 0xFE };
        const string renamedContent = "line 1\nline 2\nline 3\nline 4\nline 5\n";

        repo.WriteFile("a.txt", "old\n");
        repo.WriteFile("b.txt", renamedContent);
        repo.WriteFile("c.txt", "deleted\n");
        File.WriteAllBytes(Path.Combine(repo.Path, "bin.dat"), binaryBefore);
        await repo.GitAsync("add", "-A");
        await repo.GitAsync("commit", "-q", "-m", "root");

        repo.WriteFile("a.txt", "new\n");
        await repo.GitAsync("mv", "b.txt", "b2.txt");
        await repo.GitAsync("rm", "-q", "c.txt");
        repo.WriteFile("dir/d.txt", "added\n");
        File.WriteAllBytes(Path.Combine(repo.Path, "bin.dat"), binaryAfter);
        await repo.GitAsync("add", "-A");
        await repo.GitAsync("commit", "-q", "-m", "second");

        var log = await service.GetLogAsync(repo.Path, 10);
        CommitInfo second = log[0];
        CommitInfo root = log[1];
        var changes = await service.GetCommitChangesAsync(repo.Path, second.Sha);

        async Task<(byte[] Left, byte[] Right, FileDiff Diff)> DiffAsync(CommitInfo commit, FileChange file)
        {
            FileDiff diff = await service.GetCommitFileDiffAsync(repo.Path, commit, file);
            try
            {
                return (File.ReadAllBytes(diff.LeftFile), File.ReadAllBytes(diff.RightFile), diff);
            }
            finally
            {
                DeleteDiffFiles(diff);
            }
        }

        var modified = await DiffAsync(second, changes.Single(c => c.Path == "a.txt"));
        Assert.Equal("old\n", Encoding.UTF8.GetString(modified.Left));
        Assert.Equal("new\n", Encoding.UTF8.GetString(modified.Right));
        Assert.Equal($"a.txt ({root.Sha.Substring(0, 7)})", modified.Diff.LeftLabel);
        Assert.Equal($"a.txt ({second.Sha.Substring(0, 7)})", modified.Diff.RightLabel);

        FileChange renamedChange = changes.Single(c => c.Path == "b2.txt");
        Assert.Equal(FileChangeKind.Renamed, renamedChange.Kind);
        var renamed = await DiffAsync(second, renamedChange);
        Assert.Equal(renamedContent, Encoding.UTF8.GetString(renamed.Left));
        Assert.Equal(renamedContent, Encoding.UTF8.GetString(renamed.Right));
        Assert.Equal("b.txt", Path.GetFileName(renamed.Diff.LeftFile));
        Assert.Equal("b2.txt", Path.GetFileName(renamed.Diff.RightFile));

        var deleted = await DiffAsync(second, changes.Single(c => c.Path == "c.txt"));
        Assert.Equal("deleted\n", Encoding.UTF8.GetString(deleted.Left));
        Assert.Empty(deleted.Right);

        var added = await DiffAsync(second, changes.Single(c => c.Path == "dir/d.txt"));
        Assert.Empty(added.Left);
        Assert.Equal("added\n", Encoding.UTF8.GetString(added.Right));
        Assert.Equal("d.txt", Path.GetFileName(added.Diff.RightFile));

        var binary = await DiffAsync(second, changes.Single(c => c.Path == "bin.dat"));
        Assert.Equal(binaryBefore, binary.Left);
        Assert.Equal(binaryAfter, binary.Right);

        // ルートコミットには親がないため、変更前は空になる
        var rootChanges = await service.GetCommitChangesAsync(repo.Path, root.Sha);
        var rootDiff = await DiffAsync(root, rootChanges.Single(c => c.Path == "a.txt"));
        Assert.Empty(rootDiff.Left);
        Assert.Equal("old\n", Encoding.UTF8.GetString(rootDiff.Right));
    }

    private static void DeleteDiffFiles(FileDiff diff)
    {
        // LeftFile は <セッション>\before\<ファイル名>
        string session = Path.GetDirectoryName(Path.GetDirectoryName(diff.LeftFile)!)!;
        foreach (string file in Directory.GetFiles(session, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(session, recursive: true);
    }
}
