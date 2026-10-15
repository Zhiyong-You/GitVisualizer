using System.Linq;
using System.Text;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary><see cref="GitResult"/> の生成・連結のヘルパー。</summary>
internal static class GitResults
{
    /// <summary>git を実行せずに失敗を返すときの結果を作ります。</summary>
    public static GitResult Failure(string message) => new(1, string.Empty, message);

    /// <summary>複数回の git 実行結果を 1 つにまとめます。終了コードは最後の結果のもの。</summary>
    public static GitResult Combine(params GitResult[] results)
    {
        string Join(System.Func<GitResult, string> selector)
        {
            var sb = new StringBuilder();
            foreach (string text in results.Select(selector).Where(t => t.Length != 0))
            {
                if (sb.Length != 0 && sb[sb.Length - 1] != '\n')
                {
                    sb.Append('\n');
                }

                sb.Append(text);
            }

            return sb.ToString();
        }

        return new GitResult(results[results.Length - 1].ExitCode, Join(r => r.StandardOutput), Join(r => r.StandardError));
    }
}
