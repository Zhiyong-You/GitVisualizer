using GitVisualizer.Core.Infrastructure;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class GitOutputParserProgressTests
{
    private readonly GitOutputParser _parser = new();

    [Theory]
    [InlineData("Receiving objects:  45% (9/20)", 45)]
    [InlineData("Receiving objects:   0% (0/20)", 0)]
    [InlineData("Receiving objects: 100% (20/20), 1.20 MiB | 500.00 KiB/s, done.", 100)]
    [InlineData("Resolving deltas:  50% (1/2)", 50)]
    [InlineData("remote: Counting objects:  33% (1/3)", 33)]
    [InlineData("Writing objects:  75% (3/4), 1.00 KiB | 1.00 MiB/s", 75)]
    public void TryParseProgress_進捗率を取り出せる(string line, int expected)
    {
        Assert.True(_parser.TryParseProgress(line, out var progress));

        Assert.NotNull(progress);
        Assert.Equal(expected, progress!.Percent);
        Assert.Equal(line, progress.Message);
    }

    [Fact]
    public void TryParseProgress_CR区切りなら最後の更新を使う()
    {
        string line = "Receiving objects:  10% (2/20)\rReceiving objects:  45% (9/20)\r";

        Assert.True(_parser.TryParseProgress(line, out var progress));

        Assert.Equal(45, progress!.Percent);
        Assert.Equal("Receiving objects:  45% (9/20)", progress.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r")]
    [InlineData("remote: Enumerating objects: 5, done.")]
    [InlineData("From https://example.com/repo")]
    [InlineData("error: disk usage at 50%")]
    [InlineData(" * [new branch]      main       -> origin/main")]
    public void TryParseProgress_進捗行でなければfalse(string line)
    {
        Assert.False(_parser.TryParseProgress(line, out var progress));
        Assert.Null(progress);
    }
}
