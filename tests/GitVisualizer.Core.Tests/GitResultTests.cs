using GitVisualizer.Core.Models;
using Xunit;

namespace GitVisualizer.Core.Tests;

public class GitResultTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(128, false)]
    public void IsSuccess_終了コードが0のときだけtrue(int exitCode, bool expected)
    {
        var result = new GitResult(exitCode, standardOutput: string.Empty, standardError: string.Empty);

        Assert.Equal(expected, result.IsSuccess);
    }
}
