namespace GitVisualizer.Core.Models;

/// <summary>1 ファイル分の変更情報。</summary>
public sealed class FileChange
{
    /// <summary><see cref="FileChange"/> を初期化します。</summary>
    /// <param name="path">リポジトリルートからの相対パス。</param>
    /// <param name="kind">変更の種類。</param>
    /// <param name="isStaged">インデックスに登録済みかどうか。</param>
    /// <param name="oldPath">名前変更・コピー元のパス。該当しない場合は <see langword="null"/>。</param>
    public FileChange(string path, FileChangeKind kind, bool isStaged, string? oldPath = null)
    {
        Path = path;
        Kind = kind;
        IsStaged = isStaged;
        OldPath = oldPath;
    }

    /// <summary>リポジトリルートからの相対パス(区切りは "/")。</summary>
    public string Path { get; }

    /// <summary>名前変更・コピー元のパス。</summary>
    public string? OldPath { get; }

    /// <summary>変更の種類。</summary>
    public FileChangeKind Kind { get; }

    /// <summary>インデックスに登録済み(ステージ済み)かどうか。</summary>
    public bool IsStaged { get; }
}
