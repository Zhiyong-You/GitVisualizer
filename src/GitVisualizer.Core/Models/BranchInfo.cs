namespace GitVisualizer.Core.Models;

/// <summary>ローカル/リモートブランチの情報。</summary>
public sealed class BranchInfo
{
    /// <summary><see cref="BranchInfo"/> を初期化します。</summary>
    /// <param name="name">短い名前(例: <c>main</c>, <c>origin/main</c>)。</param>
    /// <param name="fullName">完全な参照名(例: <c>refs/heads/main</c>)。</param>
    /// <param name="tipSha">ブランチ先端のコミット SHA。</param>
    /// <param name="isRemote">リモート追跡ブランチかどうか。</param>
    /// <param name="isCurrent">現在チェックアウト中のブランチかどうか。</param>
    /// <param name="upstream">上流ブランチ名。未設定なら <see langword="null"/>。</param>
    /// <param name="ahead">上流より先行しているコミット数。</param>
    /// <param name="behind">上流より遅れているコミット数。</param>
    public BranchInfo(
        string name,
        string fullName,
        string tipSha,
        bool isRemote,
        bool isCurrent,
        string? upstream = null,
        int ahead = 0,
        int behind = 0)
    {
        Name = name;
        FullName = fullName;
        TipSha = tipSha;
        IsRemote = isRemote;
        IsCurrent = isCurrent;
        Upstream = upstream;
        Ahead = ahead;
        Behind = behind;
    }

    /// <summary>短い名前(例: <c>main</c>, <c>origin/main</c>)。</summary>
    public string Name { get; }

    /// <summary>完全な参照名(例: <c>refs/heads/main</c>)。</summary>
    public string FullName { get; }

    /// <summary>ブランチ先端のコミット SHA。</summary>
    public string TipSha { get; }

    /// <summary>リモート追跡ブランチかどうか。</summary>
    public bool IsRemote { get; }

    /// <summary>現在チェックアウト中のブランチかどうか。</summary>
    public bool IsCurrent { get; }

    /// <summary>上流ブランチ名(例: <c>origin/main</c>)。</summary>
    public string? Upstream { get; }

    /// <summary>上流より先行しているコミット数。</summary>
    public int Ahead { get; }

    /// <summary>上流より遅れているコミット数。</summary>
    public int Behind { get; }
}
