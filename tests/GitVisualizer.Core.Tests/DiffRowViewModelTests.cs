using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using GitVisualizer.Core.Services;
using GitVisualizer.UI.ViewModels;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class DiffRowViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateRows_変更前後を組にして余った行と文脈とハンクを区別する(bool staged)
    {
        const string diff =
            "diff --git a/f.txt b/f.txt\n--- a/f.txt\n+++ b/f.txt\n" +
            "@@ -1,5 +1,5 @@\n a\n-b\n-c\n+B\n d\n-e\n+E\n+F\n" +
            "@@ -10 +10 @@\n-z\n+Z\n";
        FilePatch patch = UnifiedDiffParser.Parse(
            Encoding.UTF8.GetBytes(diff), new FileChange("f.txt", FileChangeKind.Modified, staged));

        var rows = DiffRowViewModel.CreateRows(patch);
        Assert.Equal(
            new[] { DiffRowKind.HunkHeader, DiffRowKind.Context, DiffRowKind.Modified, DiffRowKind.Removed,
                DiffRowKind.Context, DiffRowKind.Modified, DiffRowKind.Added, DiffRowKind.HunkHeader, DiffRowKind.Modified },
            rows.Select(r => r.Kind));
        Assert.Equal(new[] { "b", "B" }, rows[2].ChangeLines.Select(l => l.Text));
        Assert.Equal("2", rows[2].OldNumber);
        Assert.Equal("2", rows[2].NewNumber);
        Assert.Equal("-b", rows[2].OldText);
        Assert.Equal("+B", rows[2].NewText);
        Assert.Equal(string.Empty, rows[3].NewText);
        Assert.Equal(string.Empty, rows[6].OldText);
        Assert.Empty(rows[1].ChangeLines);
        Assert.False(rows[0].HasChanges);
        Assert.False(rows[1].HasChanges);
        Assert.All(rows.Where(r => r.HasChanges), r => Assert.Equal(staged, r.IsStaged));
        var changes = rows.Where(r => r.HasChanges).SelectMany(r => r.ChangeLines).ToList();
        Assert.Equal(patch.ChangeLines.Count(), changes.Count);
        Assert.All(patch.ChangeLines, line => Assert.Single(changes, c => c == line));
        Assert.Equal(patch.Hunks[0].Lines.Where(l => l.IsChange), rows[0].ChangeLines);
    }

    private const string TwoHunkDiff =
        "diff --git a/f.txt b/f.txt\n--- a/f.txt\n+++ b/f.txt\n" +
        "@@ -1,3 +1,3 @@\n a\n-b\n+B\n-c\n" +
        "@@ -10 +10 @@\n-z\n+Z\n";

    private static IReadOnlyList<DiffRowViewModel> CreateRows(string diff, bool staged = false) =>
        DiffRowViewModel.CreateRows(UnifiedDiffParser.Parse(
            Encoding.UTF8.GetBytes(diff), new FileChange("f.txt", FileChangeKind.Modified, staged)));

    [Fact]
    public void ハンクヘッダーのチェックはそのハンクの変更行だけをまとめて切り替える()
    {
        var rows = CreateRows(TwoHunkDiff);
        DiffRowViewModel header = rows[0];
        var hunkRows = rows.Skip(1).TakeWhile(r => r.Kind != DiffRowKind.HunkHeader).Where(r => r.HasChanges).ToList();
        DiffRowViewModel otherRow = rows.Last();

        header.IsChecked = true;
        Assert.All(hunkRows, r => Assert.True(r.IsChecked));
        Assert.False(otherRow.IsChecked);

        // 1 行でも外れたらヘッダーは外れ、全部入ればまた入る
        hunkRows[0].IsChecked = false;
        Assert.False(header.IsChecked);
        hunkRows[0].IsChecked = true;
        Assert.True(header.IsChecked);

        header.IsChecked = false;
        Assert.All(hunkRows, r => Assert.False(r.IsChecked));

        // 文脈行はチェックできない
        DiffRowViewModel context = rows[1];
        context.IsChecked = true;
        Assert.False(context.IsChecked);
        Assert.False(context.IsCheckable);
    }

    [Fact]
    public void ステージ済みの差分は行のチェックボックスを出さない()
    {
        Assert.All(CreateRows(TwoHunkDiff, staged: true), r => Assert.False(r.IsCheckable));
        Assert.All(CreateRows(TwoHunkDiff).Where(r => r.HasChanges || r.Kind == DiffRowKind.HunkHeader), r => Assert.True(r.IsCheckable));
    }

    [Fact]
    public void 読み直した同じ差分では同じ行が同じキーになり_行ごとに異なる()
    {
        var keys = CreateRows(TwoHunkDiff).Select(r => r.Key).ToList();
        Assert.Equal(keys, CreateRows(TwoHunkDiff).Select(r => r.Key));

        var changeKeys = keys.Where(k => k != null).ToList();
        Assert.Equal(changeKeys.Count, changeKeys.Distinct().Count());
        Assert.Equal(3, changeKeys.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 行の組を適用すると削除と追加が一緒にステージまたは解除される(bool staged)
    {
        using var repo = await TempRepository.CreateAsync();
        var service = new CommitService(repo.Runner, repo.Parser);
        await repo.CommitFileAsync("f.txt", "a\nb\nc\nd\ne\n", "init");
        repo.WriteFile("f.txt", "a\nB1\nB2\nc\nd\nE\n");
        if (staged)
        {
            await service.StageAsync(repo.Path, new[] { "f.txt" });
        }

        FilePatch patch = await service.GetFilePatchAsync(repo.Path, Assert.Single(await service.GetStatusAsync(repo.Path)));
        DiffRowViewModel row = DiffRowViewModel.CreateRows(patch).Single(r => r.OldLine?.Text == "b");
        Assert.Equal("B1", row.NewLine?.Text);
        var lines = row.ChangeLines.ToList();
        GitResult result = staged
            ? await service.UnstageLinesAsync(repo.Path, patch, lines)
            : await service.StageLinesAsync(repo.Path, patch, lines);

        Assert.True(result.IsSuccess, result.StandardError);
        Assert.Equal(staged ? "a\nb\nB2\nc\nd\nE\n" : "a\nB1\nc\nd\ne\n",
            (await repo.GitAsync("show", ":f.txt")).StandardOutput);
        Assert.Equal("a\nB1\nB2\nc\nd\nE\n", File.ReadAllText(Path.Combine(repo.Path, "f.txt")));
    }
}
