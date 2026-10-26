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
