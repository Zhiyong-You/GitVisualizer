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
    /// <param name="input"><c>git log</c> の標準出力。</param>
    /// <returns>コミット一覧(新しい順)。</returns>
    public IReadOnlyList<CommitInfo> ParseLog(string input)
    {
        // TODO: git log -z --format="%H%x00%P%x00%an%x00%ae%x00%aI%x00%D%x00%s%x00%b" の出力を解析
        // Format: SHA\0Parents\0AuthorName\0AuthorEmail\0AuthorDateISO\0Refs\0Subject\0Body\0
        
        var commits = new List<CommitInfo>();
        
        if (string.IsNullOrEmpty(input))
        {
            return commits;
        }

        // Split by null terminator to separate commits (git log -z uses \0 as record separator)
        var records = input.Split(new[] { '\0' }, StringSplitOptions.None);
        
        // Each commit has 8 fields, so we process in groups of 8
        for (int i = 0; i + 7 < records.Length; i += 8)
        {
            var sha = records[i];
            var parentShasStr = records[i + 1];
            var authorName = records[i + 2];
            var authorEmail = records[i + 3];
            var authorDateStr = records[i + 4];
            var refsStr = records[i + 5];
            var subject = records[i + 6];
            var body = records[i + 7];

            // Parse parent SHAs (space-separated, empty string means no parents)
            var parentShas = string.IsNullOrEmpty(parentShasStr)
                ? Array.Empty<string>()
                : parentShasStr.Split(' ');

            // Parse author date (ISO 8601 format, ex: 2026-10-01T10:30:00+09:00)
            var authorDate = DateTimeOffset.Parse(authorDateStr);

            // Parse refs (format: "HEAD -> branch, tag: v1.0, origin/branch")
            var refs = ParseRefs(refsStr);

            var commit = new CommitInfo(
                sha,
                parentShas,
                authorName,
                authorEmail,
                authorDate,
                subject,
                body,
                refs);

            commits.Add(commit);
        }

        return commits;
    }

    /// <summary>作業ツリーの状態出力を解析します。</summary>
    /// <param name="output"><c>git status</c> の標準出力。</param>
    /// <returns>変更ファイル一覧(ステージ済み・未ステージの両方)。</returns>
    public IReadOnlyList<FileChange> ParseStatus(string output)
    {
        var changes = new List<FileChange>();

        if (string.IsNullOrEmpty(output))
        {
            return changes;
        }

        string[] records = output.Split(FieldSeparator);

        for (int i = 0; i < records.Length; i++)
        {
            string record = records[i];

            if (string.IsNullOrEmpty(record))
            {
                continue;
            }

            char recordType = record[0];

            switch (recordType)
            {
                case '?':
                    ParseUntrackedRecord(record, changes);
                    break;

                case '1':
                    ParseOrdinaryRecord(record, changes);
                    break;

                case '2':
                    string? oldPath = null;

                    if (i + 1 < records.Length)
                    {
                        oldPath = records[i + 1];
                        i++;
                    }

                    ParseRenamedOrCopiedRecord(record, oldPath, changes);
                    break;

                case 'u':
                    ParseUnmergedRecord(record, changes);
                    break;
            }
        }

        return changes;
    }

    private static void ParseUntrackedRecord(string record, List<FileChange> changes)
    {
        if (record.Length <= 2)
        {
            return;
        }

        string path = record.Substring(2);

        changes.Add(new FileChange(
            path,
            FileChangeKind.Untracked,
            isStaged: false));
    }

    private static void ParseOrdinaryRecord(string record, List<FileChange> changes)
    {
        string[] fields = record.Split(new[] { ' ' }, 9);

        if (fields.Length < 9)
        {
            return;
        }

        string status = fields[1];
        string path = fields[8];

        AddChangesFromStatus(
            status,
            path,
            oldPath: null,
            changes);
    }

    private static void ParseRenamedOrCopiedRecord(string record, string? oldPath, List<FileChange> changes)
    {
        string[] fields = record.Split(new[] { ' ' }, 10);

        if (fields.Length < 10)
        {
            return;
        }

        string status = fields[1];
        string changeType = fields[8];
        string path = fields[9];

        FileChangeKind kind;

        if (changeType.StartsWith("R", StringComparison.Ordinal))
        {
            kind = FileChangeKind.Renamed;
        }
        else if (changeType.StartsWith("C", StringComparison.Ordinal))
        {
            kind = FileChangeKind.Copied;
        }
        else
        {
            return;
        }

        bool isStaged = status[0] != '.';

        changes.Add(new FileChange(
            path,
            kind,
            isStaged,
            oldPath));
    }

    private static void ParseUnmergedRecord(string record, List<FileChange> changes)
    {
        string[] fields = record.Split(new[] { ' ' }, 11);

        if (fields.Length < 11)
        {
            return;
        }

        string path = fields[10];

        changes.Add(new FileChange(
            path,
            FileChangeKind.Conflicted,
            isStaged: false));
    }

    private static void AddChangesFromStatus(string status, string path, string? oldPath, List<FileChange> changes)
    {
        if (status.Length < 2)
        {
            return;
        }

        char indexStatus = status[0];
        char workTreeStatus = status[1];

        if (indexStatus != '.')
        {
            FileChangeKind? kind = ConvertStatus(indexStatus);

            if (kind.HasValue)
            {
                changes.Add(new FileChange(
                    path,
                    kind.Value,
                    isStaged: true,
                    oldPath));
            }
        }

        if (workTreeStatus != '.')
        {
            FileChangeKind? kind = ConvertStatus(workTreeStatus);

            if (kind.HasValue)
            {
                changes.Add(new FileChange(
                    path,
                    kind.Value,
                    isStaged: false,
                    oldPath));
            }
        }
    }

    private static FileChangeKind? ConvertStatus(char status)
    {
        switch (status)
        {
            case 'M':
                return FileChangeKind.Modified;

            case 'A':
                return FileChangeKind.Added;

            case 'D':
                return FileChangeKind.Deleted;

            case 'R':
                return FileChangeKind.Renamed;

            case 'C':
                return FileChangeKind.Copied;

            default:
                return null;
        }
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

    /// <summary>git log の %D フォーマット出力から参照名を解析します。</summary>
    private static IReadOnlyList<string> ParseRefs(string refsStr)
    {
        if (string.IsNullOrEmpty(refsStr))
        {
            return Array.Empty<string>();
        }

        var refs = new List<string>();
        
        // %D format: "HEAD -> branch, tag: v1.0, origin/master"
        // Split by ", " and clean up each ref
        var refParts = refsStr.Split(new[] { ", " }, StringSplitOptions.None);
        
        foreach (var refPart in refParts)
        {
            var trimmed = refPart.Trim();
            
            // Remove "tag: " prefix if present
            if (trimmed.StartsWith("tag: ", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(5);
            }
            
            if (!string.IsNullOrEmpty(trimmed))
            {
                refs.Add(trimmed);
            }
        }

        return refs;
    }
}
