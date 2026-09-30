using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>git コマンドの標準出力をモデルに変換します。</summary>
public sealed class GitOutputParser
{
    private const char FieldSeparator = '\0';
    private const string RemoteRefPrefix = "refs/remotes/";

    private static readonly Regex AheadPattern = new(@"ahead (\d+)", RegexOptions.Compiled);
    private static readonly Regex BehindPattern = new(@"behind (\d+)", RegexOptions.Compiled);

    /// <summary>ブランチ一覧の出力を解析します。</summary>
    /// <param name="output"><c>git for-each-ref</c> の標準出力。</param>
    /// <returns>ブランチ一覧。</returns>
    public IReadOnlyList<BranchInfo> ParseBranches(string output)
    {
        // 形式: %(HEAD)\0%(refname)\0%(refname:short)\0%(objectname)\0%(upstream:short)\0%(upstream:track) を 1 行 1 ref
        var branches = new List<BranchInfo>();

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(FieldSeparator);
            if (fields.Length < 6)
            {
                continue;
            }

            string fullName = fields[1];
            bool isRemote = fullName.StartsWith(RemoteRefPrefix, StringComparison.Ordinal);

            // refs/remotes/<remote>/HEAD はリモートの既定ブランチを指すシンボリック参照で、ブランチではない
            if (isRemote && fullName.EndsWith("/HEAD", StringComparison.Ordinal))
            {
                continue;
            }

            string track = fields[5];
            branches.Add(new BranchInfo(
                name: fields[2],
                fullName: fullName,
                tipSha: fields[3],
                isRemote: isRemote,
                isCurrent: fields[0] == "*",
                upstream: fields[4].Length == 0 ? null : fields[4],
                ahead: ParseTrackCount(AheadPattern, track),
                behind: ParseTrackCount(BehindPattern, track)));
        }

        return branches;
    }

    /// <summary>コミットログの出力を解析します。</summary>
    /// <param name="output"><c>git log</c> の標準出力。</param>
    /// <returns>コミット一覧(新しい順)。</returns>
    public IReadOnlyList<CommitInfo> ParseLog(string output)
    {
        // TODO: git log -z --format="%H%x00%P%x00%an%x00%ae%x00%aI%x00%D%x00%s%x00%b" の出力を解析
        throw new NotImplementedException();
    }

    /// <summary>作業ツリーの状態出力を解析します。</summary>
    /// <param name="output"><c>git status</c> の標準出力。</param>
    /// <returns>変更ファイル一覧(ステージ済み・未ステージの両方)。</returns>
    public IReadOnlyList<FileChange> ParseStatus(string output)
    {
        // TODO: git status --porcelain=v2 -z --untracked-files=all の出力を解析(1/2/u/? 行を区別)
        throw new NotImplementedException();
    }

    /// <summary>標準エラーの進捗行を解析します。</summary>
    /// <param name="line">標準エラーの 1 行(<c>\r</c> 区切りを含む)。</param>
    /// <param name="progress">解析結果。進捗行でなければ <see langword="null"/>。</param>
    /// <returns>進捗行として解析できた場合は <see langword="true"/>。</returns>
    public bool TryParseProgress(string line, out OperationProgress? progress)
    {
        // TODO: --progress 付きで出る "Receiving objects:  45% (9/20)" 形式の行から % を取り出す
        throw new NotImplementedException();
    }

    /// <summary><c>[ahead 1, behind 2]</c> 形式から指定した側の件数を取り出します。該当なしなら 0。</summary>
    private static int ParseTrackCount(Regex pattern, string track)
    {
        Match match = pattern.Match(track);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }
}
