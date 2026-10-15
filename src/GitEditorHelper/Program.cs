using System;
using System.IO;
using System.Text;

namespace GitEditorHelper;

/// <summary>git のエディタとして呼ばれる補助プログラム。</summary>
internal static class Program
{
    // GitVisualizer.Core の HistoryService と同じ名前であること
    private const string RewordShaEnvironmentVariable = "GITVISUALIZER_REWORD_SHA";
    private const string MessageFileEnvironmentVariable = "GITVISUALIZER_MESSAGE_FILE";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>エントリポイント。</summary>
    /// <param name="args">args[0] に git が編集対象ファイル(git-rebase-todo または COMMIT_EDITMSG)のパスを渡す。</param>
    /// <returns>0 なら git は処理を続行し、0 以外なら中止する。</returns>
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("GitEditorHelper: 編集対象のファイルが指定されていません。");
                return 1;
            }

            string path = args[args.Length - 1];
            return string.Equals(Path.GetFileName(path), "git-rebase-todo", StringComparison.OrdinalIgnoreCase)
                ? EditRebaseTodo(path)
                : EditCommitMessage(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("GitEditorHelper: " + ex.Message);
            return 1;
        }
    }

    /// <summary>git-rebase-todo の対象コミットの行を "pick" から "reword" に書き換えます。</summary>
    private static int EditRebaseTodo(string path)
    {
        string? sha = Environment.GetEnvironmentVariable(RewordShaEnvironmentVariable);
        if (string.IsNullOrEmpty(sha))
        {
            Console.Error.WriteLine($"GitEditorHelper: 環境変数 {RewordShaEnvironmentVariable} が設定されていません。");
            return 1;
        }

        string[] lines = File.ReadAllLines(path, Utf8NoBom);
        bool found = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string? rewritten = RewriteTodoLine(lines[i], sha!);
            if (rewritten != null)
            {
                lines[i] = rewritten;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Console.Error.WriteLine($"GitEditorHelper: リベース対象にコミット {sha} が見つかりません。");
            return 1;
        }

        File.WriteAllText(path, string.Join("\n", lines) + "\n", Utf8NoBom);
        return 0;
    }

    /// <summary>対象コミットの行なら書き換えた行を、そうでなければ <see langword="null"/> を返します。</summary>
    internal static string? RewriteTodoLine(string line, string fullSha)
    {
        // 例: "pick 1a2b3c4 メッセージ"、--rebase-merges のマージは "merge -C 1a2b3c4 label # メッセージ"
        string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && (parts[0] == "pick" || parts[0] == "p") && IsSameCommit(parts[1], fullSha))
        {
            int index = line.IndexOf(parts[0], StringComparison.Ordinal);
            return line.Substring(0, index) + "reword" + line.Substring(index + parts[0].Length);
        }

        if (parts.Length >= 3 && (parts[0] == "merge" || parts[0] == "m") && parts[1] == "-C" && IsSameCommit(parts[2], fullSha))
        {
            // -C は元のメッセージをそのまま使う、-c はエディタで編集する
            int index = line.IndexOf(" -C ", StringComparison.Ordinal);
            return line.Substring(0, index) + " -c " + line.Substring(index + 4);
        }

        return null;
    }

    private static bool IsSameCommit(string abbreviated, string fullSha)
    {
        return abbreviated.Length >= 4 && fullSha.StartsWith(abbreviated, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>COMMIT_EDITMSG を、環境変数で渡された新しいメッセージのファイルで上書きします。</summary>
    private static int EditCommitMessage(string path)
    {
        string? messageFile = Environment.GetEnvironmentVariable(MessageFileEnvironmentVariable);
        if (string.IsNullOrEmpty(messageFile) || !File.Exists(messageFile))
        {
            // 新しいメッセージの指定がなければ、元のメッセージのまま続行する
            return 0;
        }

        File.WriteAllText(path, File.ReadAllText(messageFile, Utf8NoBom), Utf8NoBom);
        return 0;
    }
}
