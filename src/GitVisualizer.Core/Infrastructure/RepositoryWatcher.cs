using System;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>リポジトリ(作業ツリーと .git ディレクトリ)の変更を監視します。</summary>
public sealed class RepositoryWatcher : IDisposable
{
    private readonly string _repositoryPath;

    /// <summary><see cref="RepositoryWatcher"/> を初期化します。</summary>
    /// <param name="repositoryPath">監視するリポジトリのルートパス。</param>
    public RepositoryWatcher(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
    }

    // TODO: 実装時に削除(未使用イベントの警告 CS0067 を抑止している)
#pragma warning disable CS0067
    /// <summary>リポジトリに変更があったときに発生します(連続した変更はまとめて 1 回通知)。</summary>
    public event EventHandler? Changed;
#pragma warning restore CS0067

    /// <summary>監視を開始します。</summary>
    public void Start()
    {
        // TODO: git rev-parse --git-dir で .git の場所を求め、FileSystemWatcher で HEAD / index / refs を監視(デバウンスする)
        throw new NotImplementedException();
    }

    /// <summary>監視を停止します。</summary>
    public void Stop()
    {
        // TODO: FileSystemWatcher の EnableRaisingEvents を false にする
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // TODO: FileSystemWatcher とデバウンス用タイマーを破棄する
        throw new NotImplementedException();
    }
}
