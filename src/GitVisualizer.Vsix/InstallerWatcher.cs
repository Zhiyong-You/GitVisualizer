using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitVisualizer.Vsix;

/// <summary>
/// VSIXInstaller の起動を監視し、VS を閉じるよう案内する。
/// VS が起動したままだと、インストーラーは理由を表示せずに VS の終了を待ち続けるため。
/// </summary>
internal sealed class InstallerWatcher : IDisposable
{
    private const string InstallerProcessName = "VSIXInstaller";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly AsyncPackage _package;
    private readonly Timer _timer;
    private readonly object _gate = new();

    // 案内済みのインストーラー(同じプロセスには 1 回だけ案内する)
    private HashSet<int> _notifiedIds = new();

    /// <summary>監視を開始します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    public InstallerWatcher(AsyncPackage package)
    {
        _package = package;
        _timer = new Timer(_ => Check(), null, Interval, Interval);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer.Dispose();
    }

    private void Check()
    {
        bool found;
        lock (_gate)
        {
            Process[] installers = Process.GetProcessesByName(InstallerProcessName);
            HashSet<int> ids = new(installers.Select(p => p.Id));
            foreach (Process installer in installers)
            {
                installer.Dispose();
            }

            found = ids.Except(_notifiedIds).Any();

            // 終了したインストーラーは忘れる(PID は再利用されるため)
            _notifiedIds = ids;
        }

        if (found)
        {
            _package.JoinableTaskFactory.RunAsync(ShowMessageAsync).FileAndForget("GitVisualizer/InstallerWatcher");
        }
    }

    private async System.Threading.Tasks.Task ShowMessageAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

        VsShellUtilities.ShowMessageBox(
            _package,
            "拡張機能のインストーラー(VSIX インストーラー)が起動しています。\n\n"
            + "インストール・更新は Visual Studio が起動している間は進みません。"
            + "作業を保存して、Visual Studio をすべて閉じてください。",
            "Git Visualizer",
            OLEMSGICON.OLEMSGICON_WARNING,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
