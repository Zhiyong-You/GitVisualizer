namespace GitEditorHelper;

/// <summary>git のエディタとして呼ばれる補助プログラム。</summary>
internal static class Program
{
    /// <summary>エントリポイント。</summary>
    /// <param name="args">args[0] に git が編集対象ファイル(git-rebase-todo または COMMIT_EDITMSG)のパスを渡す。</param>
    /// <returns>0 なら git は処理を続行し、0 以外なら中止する。</returns>
    private static int Main(string[] args)
    {
        // TODO: git-rebase-todo なら対象 SHA の行を "pick" → "reword" に、COMMIT_EDITMSG なら環境変数で渡された新メッセージのファイルで上書きする
        return 0;
    }
}
