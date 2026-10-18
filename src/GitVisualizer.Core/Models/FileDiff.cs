namespace GitVisualizer.Core.Models;

/// <summary>差分表示用に取り出した、1 ファイルの変更前・変更後の内容(一時ファイル)。</summary>
public sealed class FileDiff
{
    /// <summary><see cref="FileDiff"/> を初期化します。</summary>
    /// <param name="leftFile">変更前の内容を書き込んだファイルのフルパス(存在しない側は空ファイル)。</param>
    /// <param name="rightFile">変更後の内容を書き込んだファイルのフルパス(存在しない側は空ファイル)。</param>
    /// <param name="leftLabel">変更前の見出し。</param>
    /// <param name="rightLabel">変更後の見出し。</param>
    /// <param name="caption">差分ウィンドウのタイトル。</param>
    public FileDiff(string leftFile, string rightFile, string leftLabel, string rightLabel, string caption)
    {
        LeftFile = leftFile;
        RightFile = rightFile;
        LeftLabel = leftLabel;
        RightLabel = rightLabel;
        Caption = caption;
    }

    /// <summary>変更前の内容を書き込んだファイルのフルパス。</summary>
    public string LeftFile { get; }

    /// <summary>変更後の内容を書き込んだファイルのフルパス。</summary>
    public string RightFile { get; }

    /// <summary>変更前の見出し(例: <c>src/a.cs (1a2b3c4)</c>)。</summary>
    public string LeftLabel { get; }

    /// <summary>変更後の見出し。</summary>
    public string RightLabel { get; }

    /// <summary>差分ウィンドウのタイトル。</summary>
    public string Caption { get; }
}
