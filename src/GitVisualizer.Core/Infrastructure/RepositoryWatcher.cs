using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GitVisualizer.Core.Infrastructure;

/// <summary>リポジトリ(作業ツリーと .git ディレクトリ)の変更を監視します。</summary>
public sealed class RepositoryWatcher : IDisposable
{
    private const int DebounceMilliseconds = 500;

    // 作業ツリー内で、変更があっても通知しないフォルダ(VS やビルドが頻繁に書き込む)
    private static readonly HashSet<string> IgnoredWorkTreeFolders = new(StringComparer.OrdinalIgnoreCase) { ".vs", "bin", "obj" };

    // .git 内で、変更を通知するファイル・フォルダ(objects や logs などは無視する)
    private static readonly HashSet<string> WatchedGitEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "HEAD", "index", "refs", "packed-refs", "FETCH_HEAD", "ORIG_HEAD",
        "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply",
    };

    private readonly string _repositoryPath;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private string _gitDirectory = string.Empty;
    private bool _disposed;

    /// <summary><see cref="RepositoryWatcher"/> を初期化します。</summary>
    /// <param name="repositoryPath">監視するリポジトリのルートパス。</param>
    public RepositoryWatcher(string repositoryPath)
    {
        _repositoryPath = Path.GetFullPath(repositoryPath);
    }

    /// <summary>リポジトリに変更があったときに発生します(連続した変更はまとめて 1 回通知)。スレッドプールから呼ばれます。</summary>
    public event EventHandler? Changed;

    /// <summary>監視を開始します。</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RepositoryWatcher));
            }

            if (_watchers.Count == 0)
            {
                _gitDirectory = ResolveGitDirectory(_repositoryPath);
                _debounceTimer ??= new Timer(_ => Changed?.Invoke(this, EventArgs.Empty), null, Timeout.Infinite, Timeout.Infinite);

                _watchers.Add(CreateWatcher(_repositoryPath));

                // worktree / サブモジュールでは .git が作業ツリーの外にあるため、別に監視する
                if (!IsUnder(_gitDirectory, _repositoryPath) && Directory.Exists(_gitDirectory))
                {
                    _watchers.Add(CreateWatcher(_gitDirectory));
                }
            }

            foreach (FileSystemWatcher watcher in _watchers)
            {
                watcher.EnableRaisingEvents = true;
            }
        }
    }

    /// <summary>監視を停止します。</summary>
    public void Stop()
    {
        lock (_lock)
        {
            foreach (FileSystemWatcher watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
            }

            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (FileSystemWatcher watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    /// <summary>リポジトリの .git ディレクトリのフルパスを求めます(.git がファイルの場合は gitdir: の参照先)。</summary>
    internal static string ResolveGitDirectory(string repositoryPath)
    {
        string dotGit = Path.Combine(repositoryPath, ".git");
        if (File.Exists(dotGit))
        {
            foreach (string line in File.ReadAllLines(dotGit))
            {
                const string prefix = "gitdir:";
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string target = line.Substring(prefix.Length).Trim();
                    return Path.GetFullPath(Path.Combine(repositoryPath, target));
                }
            }
        }

        return dotGit;
    }

    private FileSystemWatcher CreateWatcher(string path)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
        };

        watcher.Changed += OnFileSystemEvent;
        watcher.Created += OnFileSystemEvent;
        watcher.Deleted += OnFileSystemEvent;
        watcher.Renamed += OnFileSystemEvent;

        // バッファあふれなどで取りこぼした場合は、念のため通知する
        watcher.Error += (_, _) => ScheduleNotification();
        return watcher;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (ShouldNotify(e.FullPath) || (e is RenamedEventArgs renamed && ShouldNotify(renamed.OldFullPath)))
        {
            ScheduleNotification();
        }
    }

    private void ScheduleNotification()
    {
        lock (_lock)
        {
            // 最後の変更から一定時間たってから 1 回だけ通知する
            _debounceTimer?.Change(DebounceMilliseconds, Timeout.Infinite);
        }
    }

    private bool ShouldNotify(string fullPath)
    {
        if (IsUnder(fullPath, _gitDirectory))
        {
            string relative = fullPath.Substring(_gitDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relative.Length == 0 || relative.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            return WatchedGitEntries.Contains(first);
        }

        if (!IsUnder(fullPath, _repositoryPath))
        {
            return false;
        }

        string workTreeRelative = fullPath.Substring(_repositoryPath.Length);
        foreach (string segment in workTreeRelative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (IgnoredWorkTreeFolders.Contains(segment))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnder(string path, string directory)
    {
        if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Length == directory.Length
            || path[directory.Length] == Path.DirectorySeparatorChar
            || path[directory.Length] == Path.AltDirectorySeparatorChar;
    }
}
