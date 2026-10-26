using System.Linq;
using System.Text;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;
using Xunit;

namespace GitVisualizer.Core.Tests;

/// <summary><see cref="UnifiedDiffParser"/> と <see cref="LinePatchBuilder"/> のテスト。</summary>
public class LinePatchBuilderTests
{
    private const string ModifiedDiff =
        "diff --git a/f.txt b/f.txt\n" +
        "index 9405325..0f77ba4 100644\n" +
        "--- a/f.txt\n" +
        "+++ b/f.txt\n" +
        "@@ -1,5 +1,6 @@\n" +
        " a\n" +
        "-b\n" +
        "+B1\n" +
        "+B2\n" +
        " c\n" +
        " d\n" +
        "-e\n" +
        "+E\n";

    private static FilePatch Parse(string diff, bool isStaged = false) =>
        UnifiedDiffParser.Parse(Encoding.UTF8.GetBytes(diff), new FileChange("f.txt", FileChangeKind.Modified, isStaged));

    private static string Build(FilePatch patch, bool reverse, params string[] selectedTexts)
    {
        var selected = patch.ChangeLines.Where(l => selectedTexts.Contains(l.Text));
        byte[]? bytes = LinePatchBuilder.Build(patch, selected, reverse);
        return bytes == null ? "(null)" : Encoding.UTF8.GetString(bytes);
    }

    [Fact]
    public void Parse_行の種類と行番号()
    {
        FilePatch patch = Parse(ModifiedDiff);

        DiffHunk hunk = Assert.Single(patch.Hunks);
        Assert.Equal("@@ -1,5 +1,6 @@", hunk.Header);
        Assert.Equal(
            new[] { DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added, DiffLineKind.Added, DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added },
            hunk.Lines.Select(l => l.Kind));
        Assert.Equal(new int?[] { 1, 2, null, null, 3, 4, 5, null }, hunk.Lines.Select(l => l.OldLineNumber));
        Assert.Equal(new int?[] { 1, null, 2, 3, 4, 5, null, 6 }, hunk.Lines.Select(l => l.NewLineNumber));
        Assert.True(patch.CanApplyPartially);
    }

    [Fact]
    public void Build_ステージ_選択しない追加行は削除し削除行は文脈行にする()
    {
        string result = Build(Parse(ModifiedDiff), reverse: false, "b", "B1");

        // 一部だけ適用する場合、内容と合わなくなる index 行は除く
        Assert.Equal(
            "diff --git a/f.txt b/f.txt\n" +
            "--- a/f.txt\n" +
            "+++ b/f.txt\n" +
            "@@ -1,5 +1,6 @@\n" +
            " a\n" +
            "-b\n" +
            "+B1\n" +
            " c\n" +
            " d\n" +
            " e\n",
            result);
    }

    [Fact]
    public void Build_ステージ解除_選択しない追加行は文脈行にし削除行は削除する()
    {
        string result = Build(Parse(ModifiedDiff, isStaged: true), reverse: true, "B2");

        Assert.EndsWith(
            "@@ -1,5 +1,6 @@\n" +
            " a\n" +
            " B1\n" +
            "+B2\n" +
            " c\n" +
            " d\n" +
            " E\n",
            result);
    }

    [Fact]
    public void Build_選択した行がないハンクは出力しない()
    {
        const string diff =
            "diff --git a/f.txt b/f.txt\n" +
            "--- a/f.txt\n" +
            "+++ b/f.txt\n" +
            "@@ -1,2 +1,2 @@\n" +
            "-a\n" +
            "+A\n" +
            " b\n" +
            "@@ -10,2 +10,2 @@ func\n" +
            " x\n" +
            "-y\n" +
            "+Y\n";

        string result = Build(Parse(diff), reverse: false, "Y");

        Assert.DoesNotContain("@@ -1,2", result);
        Assert.EndsWith("@@ -10,2 +10,2 @@ func\n x\n y\n+Y\n", result);
        Assert.Equal("(null)", Build(Parse(diff), reverse: false));
    }

    [Fact]
    public void Build_改行なしの印とCRLFを保持する()
    {
        const string diff =
            "diff --git a/f.txt b/f.txt\n" +
            "--- a/f.txt\n" +
            "+++ b/f.txt\n" +
            "@@ -1,2 +1,2 @@\n" +
            " a\r\n" +
            "-b\r\n" +
            "\\ No newline at end of file\n" +
            "+B\r\n" +
            "\\ No newline at end of file\n";

        FilePatch patch = Parse(diff);
        Assert.Equal("a", patch.Hunks[0].Lines[0].Text);
        Assert.True(patch.Hunks[0].Lines[1].NoNewlineAtEnd);

        // 全行を選ぶと元の diff と同じになる
        string all = Build(patch, reverse: false, "b", "B");
        Assert.Equal(diff, all);
    }

    [Fact]
    public void Build_新規ファイルの一部だけなら通常の変更のヘッダーにする()
    {
        const string diff =
            "diff --git a/n.txt b/n.txt\n" +
            "new file mode 100644\n" +
            "index 0000000..04ec35a\n" +
            "--- /dev/null\n" +
            "+++ b/n.txt\n" +
            "@@ -0,0 +1,3 @@\n" +
            "+x\n" +
            "+y\n" +
            "+z\n";

        FilePatch patch = Parse(diff);
        Assert.True(patch.IsNewFile);

        Assert.Equal(
            "diff --git a/n.txt b/n.txt\n" +
            "--- a/n.txt\n" +
            "+++ b/n.txt\n" +
            "@@ -0,0 +1,3 @@\n" +
            "+x\n" +
            "+z\n",
            Build(patch, reverse: false, "x", "z"));

        // すべて選んだ場合は元のヘッダーのまま
        Assert.Equal(diff, Build(patch, reverse: false, "x", "y", "z"));
    }

    [Fact]
    public void Build_ステージ済みの削除は一部だけ適用できない()
    {
        const string diff =
            "diff --git a/d.txt b/d.txt\n" +
            "deleted file mode 100644\n" +
            "--- a/d.txt\n" +
            "+++ /dev/null\n" +
            "@@ -1,2 +0,0 @@\n" +
            "-1\n" +
            "-2\n";

        FilePatch patch = UnifiedDiffParser.Parse(Encoding.UTF8.GetBytes(diff), new FileChange("d.txt", FileChangeKind.Deleted, isStaged: true));

        Assert.False(patch.CanApplyPartially);
        Assert.Throws<System.InvalidOperationException>(() => LinePatchBuilder.Build(patch, patch.ChangeLines.Take(1), reverse: true));
        Assert.NotNull(LinePatchBuilder.Build(patch, patch.ChangeLines, reverse: true));
    }

    [Fact]
    public void Parse_UTF8でなければShiftJISとして表示し元のバイト列で組み立てる()
    {
        Encoding sjis = Encoding.GetEncoding(932);
        byte[] bytes = sjis.GetBytes(
            "diff --git a/f.txt b/f.txt\n" +
            "--- a/f.txt\n" +
            "+++ b/f.txt\n" +
            "@@ -1 +1 @@\n" +
            "-あいう\n" +
            "+かきく\n");

        FilePatch patch = UnifiedDiffParser.Parse(bytes, new FileChange("f.txt", FileChangeKind.Modified, isStaged: false));

        Assert.Equal(new[] { "あいう", "かきく" }, patch.ChangeLines.Select(l => l.Text));
        Assert.Equal(bytes, LinePatchBuilder.Build(patch, patch.ChangeLines, reverse: false));
    }

    [Fact]
    public void Parse_バイナリと空の差分()
    {
        FilePatch binary = Parse("diff --git a/f.bin b/f.bin\nindex 1..2 100644\nBinary files a/f.bin and b/f.bin differ\n");
        Assert.True(binary.IsBinary);
        Assert.False(binary.CanApplyLines);

        FilePatch empty = UnifiedDiffParser.Parse(new byte[0], new FileChange("f.txt", FileChangeKind.Modified, isStaged: false));
        Assert.Empty(empty.Hunks);
        Assert.False(empty.CanApplyLines);
    }
}
