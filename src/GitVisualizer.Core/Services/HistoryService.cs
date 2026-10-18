using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Infrastructure;
using GitVisualizer.Core.Models;

namespace GitVisualizer.Core.Services;

/// <summary><see cref="IHistoryService"/> の git.exe 実装。</summary>
public sealed class HistoryService : IHistoryService
{
    /// <summary>reword 対象のコミット SHA を GitEditorHelper に渡す環境変数名。</summary>
    public const string RewordShaEnvironmentVariable = "GITVISUALIZER_REWORD_SHA";

    /// <summary>新しいコミットメッセージのファイルパスを GitEditorHelper に渡す環境変数名。</summary>
    public const string MessageFileEnvironmentVariable = "GITVISUALIZER_MESSAGE_FILE";

    private const string LogFormat = "--format=%H%x00%P%x00%an%x00%ae%x00%aI%x00%D%x00%s%x00%b";

    private readonly IGitCommandRunner _runner;
    private readonly GitOutputParser _parser;
    private readonly IRepositoryStateService _state;
    private readonly string _editorHelperPath;

    /// <summary><see cref="HistoryService"/> を初期化します。</summary>
    /// <param name="runner">git 実行器。</param>
    /// <param name="parser">出力パーサー。</param>
    /// <param name="state">中断中の操作を判定するためのサービス。</param>
    /// <param name="editorHelperPath">GitEditorHelper.exe のフルパス(reword で GIT_SEQUENCE_EDITOR / GIT_EDITOR に使う)。</param>
    public HistoryService(IGitCommandRunner runner, GitOutputParser parser, IRepositoryStateService state, string editorHelperPath)
    {
        _runner = runner;
        _parser = parser;
        _state = state;
        _editorHelperPath = editorHelperPath;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommitInfo>> GetLogAsync(string repositoryPath, int maxCount, CancellationToken ct = default)
    {
        // --all だと refs/stash まで表示されるため、ブランチ・リモート・タグ・HEAD を明示する
        var arguments = new List<string>
        {
            "log", "--branches", "--remotes", "--tags", "--topo-order",
            "-n", maxCount.ToString(CultureInfo.InvariantCulture), "-z", LogFormat,
        };

        // コミットが 1 つもないリポジトリでは HEAD を指定するとエラーになる
        if (await RevParseAsync(repositoryPath, "HEAD", ct).ConfigureAwait(false) != null)
        {
            arguments.Add("HEAD");
        }

        GitResult result = await _runner.RunAsync(repositoryPath, arguments, ct: ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError);
        }

        return _parser.ParseLog(result.StandardOutput);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileChange>> GetCommitChangesAsync(string repositoryPath, string commitSha, CancellationToken ct = default)
    {
        GitResult result = await _runner.RunAsync(
            repositoryPath,
            new[] { "show", "--format=", "--name-status", "-z", "-M", "--diff-merges=first-parent", commitSha },
            ct: ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.StandardError);
        }

        return _parser.ParseNameStatus(result.StandardOutput);
    }

    /// <inheritdoc/>
    public async Task<FileDiff> GetCommitFileDiffAsync(string repositoryPath, CommitInfo commit, FileChange file, CancellationToken ct = default)
    {
        // マージコミットは GetCommitChangesAsync(--diff-merges=first-parent)に合わせて第 1 親と比べる。ルートコミットは親なし
        string? parentSha = commit.ParentShas.Count > 0 ? commit.ParentShas[0] : null;
        string oldPath = file.OldPath ?? file.Path;
        bool hasLeft = parentSha != null && file.Kind != FileChangeKind.Added && file.Kind != FileChangeKind.Untracked;
        bool hasRight = file.Kind != FileChangeKind.Deleted;

        // 拡張子で VS の言語(色分け)が決まるため、ファイル名はそのまま使い、変更前・変更後はフォルダで分ける
        string session = DiffTempDirectory.CreateSessionDirectory();
        string leftFile = Path.Combine(session, "before", GetFileName(oldPath));
        string rightFile = Path.Combine(session, "after", GetFileName(file.Path));

        await WriteBlobAsync(repositoryPath, hasLeft ? parentSha + ":" + oldPath : null, leftFile, ct).ConfigureAwait(false);
        await WriteBlobAsync(repositoryPath, hasRight ? commit.Sha + ":" + file.Path : null, rightFile, ct).ConfigureAwait(false);

        string shortSha = ShortSha(commit.Sha);
        string leftLabel = hasLeft ? $"{oldPath} ({ShortSha(parentSha!)})" : $"{oldPath} (変更前なし)";
        string rightLabel = hasRight ? $"{file.Path} ({shortSha})" : $"{file.Path} (削除)";
        return new FileDiff(leftFile, rightFile, leftLabel, rightLabel, $"{GetFileName(file.Path)} ({shortSha})");
    }

    /// <summary><c>&lt;rev&gt;:&lt;path&gt;</c> の内容を読み取り専用のファイルに書き出します。<paramref name="blob"/> が null なら空ファイル。</summary>
    private async Task WriteBlobAsync(string repositoryPath, string? blob, string destination, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (blob == null)
        {
            File.WriteAllBytes(destination, Array.Empty<byte>());
        }
        else
        {
            // --filters: チェックアウト時と同じ変換(改行コード、smudge フィルタなど)をかけ、作業ツリーのファイルと同じ形にする
            GitResult result = await _runner.RunToFileAsync(repositoryPath, new[] { "cat-file", "--filters", blob }, destination, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.StandardError);
            }
        }

        // 一時ファイルを編集しても元のファイルには反映されないため、読み取り専用にする
        File.SetAttributes(destination, FileAttributes.ReadOnly);
    }

    private static string GetFileName(string repositoryRelativePath)
    {
        int slash = repositoryRelativePath.LastIndexOf('/');
        return slash < 0 ? repositoryRelativePath : repositoryRelativePath.Substring(slash + 1);
    }

    private static string ShortSha(string sha) => sha.Length > 7 ? sha.Substring(0, 7) : sha;

    /// <inheritdoc/>
    public Task<GitResult> RebaseAsync(string repositoryPath, RebaseOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Onto))
        {
            return Task.FromResult(GitResults.Failure("リベース先が指定されていません。"));
        }

        var arguments = new List<string> { "rebase" };
        if (options.AutoStash)
        {
            arguments.Add("--autostash");
        }

        arguments.Add(options.Onto.Trim());
        return _runner.RunAsync(repositoryPath, arguments, ct: ct);
    }

    /// <inheritdoc/>
    public Task<GitResult> CherryPickAsync(string repositoryPath, IReadOnlyList<string> commitShas, CherryPickOptions options, CancellationToken ct = default)
    {
        if (commitShas.Count == 0)
        {
            return Task.FromResult(GitResults.Failure("取り込むコミットが指定されていません。"));
        }

        var arguments = new List<string> { "cherry-pick" };
        if (options.NoCommit)
        {
            arguments.Add("--no-commit");
        }

        if (options.RecordOrigin)
        {
            arguments.Add("-x");
        }

        if (options.Mainline is int mainline)
        {
            arguments.Add("-m");
            arguments.Add(mainline.ToString(CultureInfo.InvariantCulture));
        }

        arguments.AddRange(commitShas);
        return _runner.RunAsync(repositoryPath, arguments, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<GitResult> RewordAsync(string repositoryPath, string commitSha, string newMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newMessage))
        {
            return GitResults.Failure("コミットメッセージが空です。");
        }

        string? head = await RevParseAsync(repositoryPath, "HEAD", ct).ConfigureAwait(false);
        string? target = await RevParseAsync(repositoryPath, commitSha + "^{commit}", ct).ConfigureAwait(false);
        if (head == null || target == null)
        {
            return GitResults.Failure($"コミット {commitSha} が見つかりません。");
        }

        using var messageFile = new TempMessageFile(newMessage);

        if (target == head)
        {
            // HEAD ならメッセージだけを修正する(--only: ステージ済みの変更は含めない)
            return await _runner.RunAsync(
                repositoryPath,
                new[] { "commit", "--amend", "--only", "--allow-empty", "-F", messageFile.Path },
                ct: ct).ConfigureAwait(false);
        }

        // 現在のブランチに含まれないコミットは書き換えられない
        GitResult ancestor = await _runner.RunAsync(repositoryPath, new[] { "merge-base", "--is-ancestor", target, "HEAD" }, ct: ct).ConfigureAwait(false);
        if (!ancestor.IsSuccess)
        {
            return GitResults.Failure("選択したコミットは現在のブランチに含まれていないため、メッセージを変更できません。");
        }

        // 親がなければ(ルートコミット)--root、あればその親からの対話的リベース
        GitResult parents = await _runner.RunAsync(repositoryPath, new[] { "rev-list", "--parents", "-n", "1", target }, ct: ct).ConfigureAwait(false);
        bool isRoot = parents.StandardOutput.Trim().Split(' ').Length <= 1;

        // GitEditorHelper が git-rebase-todo の対象行を reword に変え、COMMIT_EDITMSG を新メッセージで上書きする。
        // git はエディタを sh 経由で起動するため、パスは引用符で囲み "/" 区切りにする
        string editor = "\"" + _editorHelperPath.Replace('\\', '/') + "\"";
        var environment = new Dictionary<string, string>
        {
            ["GIT_SEQUENCE_EDITOR"] = editor,
            ["GIT_EDITOR"] = editor,
            [RewordShaEnvironmentVariable] = target,
            [MessageFileEnvironmentVariable] = messageFile.Path,
        };

        var arguments = new List<string> { "rebase", "--interactive", "--autostash", "--rebase-merges" };
        arguments.Add(isRoot ? "--root" : target + "^");

        return await _runner.RunAsync(repositoryPath, arguments, environment, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<GitResult> ContinueAsync(string repositoryPath, CancellationToken ct = default)
    {
        // --continue で開くエディタは、GitProcessRunner の既定(GIT_EDITOR=true)で抑止される
        string? command = await GetOperationCommandAsync(repositoryPath, ct).ConfigureAwait(false);
        if (command == null)
        {
            return GitResults.Failure("続行できる操作はありません。");
        }

        return await _runner.RunAsync(repositoryPath, new[] { command, "--continue" }, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<GitResult> SkipAsync(string repositoryPath, CancellationToken ct = default)
    {
        RepositoryOperation operation = await _state.GetOperationAsync(repositoryPath, ct).ConfigureAwait(false);
        if (operation == RepositoryOperation.Merge)
        {
            return GitResults.Failure("マージはスキップできません。続行するか中止してください。");
        }

        string? command = ToCommand(operation);
        if (command == null)
        {
            return GitResults.Failure("スキップできる操作はありません。");
        }

        return await _runner.RunAsync(repositoryPath, new[] { command, "--skip" }, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<GitResult> AbortAsync(string repositoryPath, CancellationToken ct = default)
    {
        string? command = await GetOperationCommandAsync(repositoryPath, ct).ConfigureAwait(false);
        if (command == null)
        {
            return GitResults.Failure("中止できる操作はありません。");
        }

        return await _runner.RunAsync(repositoryPath, new[] { command, "--abort" }, ct: ct).ConfigureAwait(false);
    }

    private async Task<string?> GetOperationCommandAsync(string repositoryPath, CancellationToken ct)
    {
        RepositoryOperation operation = await _state.GetOperationAsync(repositoryPath, ct).ConfigureAwait(false);
        return ToCommand(operation);
    }

    private static string? ToCommand(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.Rebase => "rebase",
        RepositoryOperation.CherryPick => "cherry-pick",
        RepositoryOperation.Revert => "revert",
        RepositoryOperation.Merge => "merge",
        _ => null,
    };

    private async Task<string?> RevParseAsync(string repositoryPath, string revision, CancellationToken ct)
    {
        GitResult result = await _runner.RunAsync(repositoryPath, new[] { "rev-parse", "--verify", "--quiet", revision }, ct: ct).ConfigureAwait(false);
        return result.IsSuccess ? result.StandardOutput.Trim() : null;
    }
}
