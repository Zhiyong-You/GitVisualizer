using GitVisualizer.Core.Infrastructure;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class GitIgnoreFileTests
{
    [Theory]
    [InlineData("a.txt", "/a.txt")]
    [InlineData("src/#a.txt", "/src/#a.txt")]
    [InlineData("src/!a*?.txt", @"/src/!a\*\?.txt")]
    [InlineData(@"a[1]\b.txt", @"/a\[1]\\b.txt")]
    [InlineData("dir/name ", @"/dir/name\ ")]
    public void PatternForPath_特殊文字をエスケープしてルートからのパターンにする(string path, string expected)
    {
        Assert.Equal(expected, GitIgnoreFile.PatternForPath(path));
    }

    [Theory]
    [InlineData("src/a.txt", "*.txt")]
    [InlineData("a.tar.gz", "*.gz")]
    [InlineData("dir.v2/Makefile", null)]
    [InlineData(".gitattributes", null)]
    [InlineData("a.", null)]
    public void PatternForExtension_拡張子のパターンを作る(string path, string? expected)
    {
        Assert.Equal(expected, GitIgnoreFile.PatternForExtension(path));
    }
}
