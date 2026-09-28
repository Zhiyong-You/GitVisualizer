using System;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>使用する git.exe のフルパスを探します。</summary>
public sealed class GitExeLocator
{
    /// <summary>git.exe のフルパスを返します。</summary>
    /// <returns>見つかった git.exe のフルパス。見つからない場合は <see langword="null"/>。</returns>
    public string? FindGitExecutable()
    {
        // TODO: PATH → VS 同梱 Git → Program Files\Git の順に探し、git --version で動作確認する
        throw new NotImplementedException();
    }
}
