using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="ICommitService"/> の git.exe 実装。</summary>
public sealed class CommitService : ICommitService
{
    // 利用者の設定(色付け・外部 diff ツール・textconv・接頭辞・名前変更の検出)に左右されない、git apply で戻せる形の diff にする
    private static readonly string[] DiffOptions =
    {
        "diff", "--no-color", "--no-ext-diff", "--no-textconv", "--no-renames", "--src-prefix=a/", "--dst-prefix=b/", "-U3",
    };

    private readonly IGitCommandRunner _runner;
    private readonly GitOutputParser _parser;

    /// <summary><see cref="CommitService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    /// <param name="parser">出力パーサー。</param>
    public CommitService(IGitCommandRunner runner, GitOutputParser parser)
    {
        _runner = runner;
        _parser = parser;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileChange>> GetStatusAsync(string repositoryPath, CancellationToken ct = default)
    {
        // --no-optional-locks: status がインデックスを書き換えないようにする(書き換えると RepositoryWatcher が反応し、更新が繰り返される)
        GitResult result = await _runner.RunAsync(
            repositoryPath,
            new[] { "--no-optional-locks", "status", "--porcelain=v2", "-z", "--untracked-files=all" },
            ct: ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError);
        }

        return _parser.ParseStatus(result.StandardOutput);
    }

    /// <inheritdoc/>
    public Task<GitResult> StageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return Task.FromResult(new GitResult(0, string.Empty, string.Empty));
        }

        var arguments = new List<string> { "add", "--" };
        arguments.AddRange(paths);
        return _runner.RunAsync(repositoryPath, arguments, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<GitResult> UnstageAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0)
        {
            return new GitResult(0, string.Empty, string.Empty);
        }

        // コミットがまだ 1 つもないリポジトリでは restore --staged が使えないため、rm --cached で外す
        GitResult head = await _runner.RunAsync(repositoryPath, new[] { "rev-parse", "--verify", "--quiet", "HEAD" }, ct: ct).ConfigureAwait(false);

        var arguments = head.IsSuccess
            ? new List<string> { "restore", "--staged", "--" }
            : new List<string> { "rm", "--cached", "--quiet", "-r", "--" };
        arguments.AddRange(paths);
        return await _runner.RunAsync(repositoryPath, arguments, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<FilePatch> GetFilePatchAsync(string repositoryPath, FileChange change, CancellationToken ct = default)
    {
        // 未追跡ファイルは git diff に出ないため、空のファイルと比べる(--no-index は差分があると終了コード 1)
        bool isUntracked = change.Kind == FileChangeKind.Untracked;
        var arguments = new List<string>(DiffOptions);
        if (isUntracked)
        {
            arguments.AddRange(new[] { "--no-index", "--", "/dev/null", change.Path });
        }
        else
        {
            if (change.IsStaged)
            {
                arguments.Add("--cached");
            }

            arguments.Add("--");
            arguments.Add(change.Path);
        }

        // 出力は Shift-JIS などのこともあるため、バイト列のまま受け取る
        using var output = new TempPatchFile();
        GitResult result = await _runner.RunToFileAsync(repositoryPath, arguments, output.Path, ct).ConfigureAwait(false);
        if (!result.IsSuccess && !(isUntracked && result.ExitCode == 1))
        {
            throw new InvalidOperationException(result.StandardError);
        }

        return UnifiedDiffParser.Parse(output.ReadAllBytes(), change);
    }

    /// <inheritdoc/>
    public async Task<FileDiff> GetFileDiffAsync(string repositoryPath, FileChange change, CancellationToken ct = default)
    {
        string oldPath = change.OldPath ?? change.Path;
        bool hasLeft = change.Kind != FileChangeKind.Untracked && !(change.IsStaged && change.Kind == FileChangeKind.Added);
        bool hasRight = change.Kind != FileChangeKind.Deleted;
        // 競合中は通常のインデックス(stage 0)がないため、HEAD と作業ツリーを比較する。
        string leftRevision = change.IsStaged || change.Kind == FileChangeKind.Conflicted ? "HEAD" : string.Empty;
        if (change.Kind == FileChangeKind.Conflicted)
        {
            GitResult exists = await _runner.RunAsync(repositoryPath, new[] { "cat-file", "-e", "HEAD:" + oldPath }, ct: ct).ConfigureAwait(false);
            hasLeft = exists.IsSuccess;
            hasRight = File.Exists(Path.Combine(repositoryPath, change.Path));
        }

        string session = DiffTempDirectory.CreateSessionDirectory();
        string leftFile = Path.Combine(session, "before", Path.GetFileName(oldPath));
        string rightFile = Path.Combine(session, "after", Path.GetFileName(change.Path));

        await WriteDiffFileAsync(repositoryPath, hasLeft ? leftRevision + ":" + oldPath : null, null, leftFile, ct).ConfigureAwait(false);
        await WriteDiffFileAsync(
            repositoryPath,
            hasRight && change.IsStaged ? ":" + change.Path : null,
            hasRight && !change.IsStaged ? Path.Combine(repositoryPath, change.Path) : null,
            rightFile,
            ct).ConfigureAwait(false);

        string leftLabel = $"{oldPath} ({(hasLeft ? leftRevision == "HEAD" ? "HEAD" : "インデックス" : "変更前なし")})";
        string rightLabel = $"{change.Path} ({(hasRight ? change.IsStaged ? "インデックス" : "作業ツリー" : "削除")})";
        return new FileDiff(leftFile, rightFile, leftLabel, rightLabel, $"{Path.GetFileName(change.Path)} ({(change.IsStaged ? "ステージ済み" : "未ステージ")})");
    }

    private async Task WriteDiffFileAsync(string repositoryPath, string? blob, string? sourceFile, string destination, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (blob != null)
        {
            GitResult result = await _runner.RunToFileAsync(repositoryPath, new[] { "cat-file", "--filters", blob }, destination, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.StandardError);
            }
        }
        else if (sourceFile != null)
        {
            File.Copy(sourceFile, destination);
        }
        else
        {
            File.WriteAllBytes(destination, Array.Empty<byte>());
        }

        File.SetAttributes(destination, FileAttributes.ReadOnly);
    }

    /// <inheritdoc/>
    public async Task<GitResult> StageLinesAsync(string repositoryPath, FilePatch patch, IReadOnlyCollection<DiffLine> lines, CancellationToken ct = default)
    {
        if (patch.Change.IsStaged)
        {
            return GitResults.Failure("ステージ済みの差分はステージできません。");
        }

        if (patch.Change.Kind != FileChangeKind.Untracked)
        {
            return await ApplyLinesAsync(repositoryPath, patch, lines, reverse: false, ct).ConfigureAwait(false);
        }

        // 未追跡ファイルは、空の内容でインデックスに登録(intent-to-add)してから、git diff の結果に選択を移して適用する
        GitResult intent = await _runner.RunAsync(repositoryPath, new[] { "add", "--intent-to-add", "--", patch.Change.Path }, ct: ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
        {
            return intent;
        }

        GitResult result;
        try
        {
            FilePatch tracked = await GetFilePatchAsync(repositoryPath, new FileChange(patch.Change.Path, FileChangeKind.Added, isStaged: false), ct).ConfigureAwait(false);
            IReadOnlyCollection<DiffLine>? mapped = MapSelection(patch, tracked, lines);
            result = mapped == null
                ? GitResults.Failure("ファイルの内容が変わったため、選択した行をステージできませんでした。再読み込みしてからやり直してください。")
                : await ApplyLinesAsync(repositoryPath, tracked, mapped, reverse: false, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = GitResults.Failure(ex.Message);
        }

        if (!result.IsSuccess)
        {
            // 失敗したら intent-to-add を取り消して、未追跡の状態に戻す
            await _runner.RunAsync(repositoryPath, new[] { "rm", "--cached", "--quiet", "--", patch.Change.Path }, ct: CancellationToken.None).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<GitResult> UnstageLinesAsync(string repositoryPath, FilePatch patch, IReadOnlyCollection<DiffLine> lines, CancellationToken ct = default)
    {
        if (!patch.Change.IsStaged)
        {
            return Task.FromResult(GitResults.Failure("未ステージの差分はステージ解除できません。"));
        }

        return ApplyLinesAsync(repositoryPath, patch, lines, reverse: true, ct);
    }

    private async Task<GitResult> ApplyLinesAsync(string repositoryPath, FilePatch patch, IReadOnlyCollection<DiffLine> lines, bool reverse, CancellationToken ct)
    {
        if (!patch.CanApplyLines)
        {
            return GitResults.Failure("このファイルは行単位で操作できません(バイナリ・競合中など)。ファイル単位で操作してください。");
        }

        byte[]? content;
        try
        {
            content = LinePatchBuilder.Build(patch, lines, reverse);
        }
        catch (InvalidOperationException ex)
        {
            return GitResults.Failure(ex.Message);
        }

        if (content == null)
        {
            return GitResults.Failure("追加行・削除行が選択されていません。");
        }

        // git の標準入力は閉じているため、パッチは一時ファイルで渡す。行数は --recount で数え直させる
        using var patchFile = new TempPatchFile(content);
        var arguments = new List<string> { "apply", "--cached", "--recount", "--whitespace=nowarn" };
        if (reverse)
        {
            arguments.Add("--reverse");
        }

        arguments.Add(patchFile.Path);
        return await _runner.RunAsync(repositoryPath, arguments, ct: ct).ConfigureAwait(false);
    }

    /// <summary>同じ形の diff の間で、選択した行を位置(ハンク番号・行番号)で対応付けます。形が違えば <see langword="null"/>。</summary>
    private static IReadOnlyCollection<DiffLine>? MapSelection(FilePatch from, FilePatch to, IReadOnlyCollection<DiffLine> lines)
    {
        if (from.Hunks.Count != to.Hunks.Count)
        {
            return null;
        }

        var selected = new HashSet<DiffLine>(lines);
        var mapped = new List<DiffLine>();
        for (int h = 0; h < from.Hunks.Count; h++)
        {
            IReadOnlyList<DiffLine> source = from.Hunks[h].Lines;
            IReadOnlyList<DiffLine> target = to.Hunks[h].Lines;
            if (source.Count != target.Count)
            {
                return null;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Kind != target[i].Kind)
                {
                    return null;
                }

                if (selected.Contains(source[i]))
                {
                    mapped.Add(target[i]);
                }
            }
        }

        return mapped;
    }

    /// <inheritdoc/>
    public async Task<GitResult> CommitAsync(string repositoryPath, string message, bool amend, CancellationToken ct = default)
    {
        // メッセージは引数だと改行や引用符の扱いが難しいため、一時ファイル(UTF-8)経由で渡す
        using var messageFile = new TempMessageFile(message);

        var arguments = new List<string> { "commit" };
        if (amend)
        {
            arguments.Add("--amend");
        }

        arguments.Add("-F");
        arguments.Add(messageFile.Path);

        return await _runner.RunAsync(repositoryPath, arguments, ct: ct).ConfigureAwait(false);
    }
}
