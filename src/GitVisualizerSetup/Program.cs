using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GitVisualizerSetup;

/// <summary>インストールを妨げるプロセスが終わったことを確認してから、埋め込んだ .vsix でインストーラーを開くセットアップ。</summary>
internal static class Program
{
    private const string Caption = "Git Visualizer セットアップ";
    private const string VsixResourceName = "GitVisualizer.vsix";

    private const string MSBuildProcessName = "MSBuild";

    // VSIXInstaller が終了を待つプロセス(MSBuild は VS を閉じた後もしばらく残ることがある)
    private static readonly (string ProcessName, string DisplayName)[] BlockingProcesses =
    {
        ("devenv", "Visual Studio (devenv.exe)"),
        (MSBuildProcessName, "MSBuild.exe"),
    };

    [STAThread]
    private static int Main()
    {
        try
        {
            if (!WaitForBlockingProcesses())
            {
                return 1;
            }

            // 関連付けで VSIXInstaller が開く。同じ Id で Version が上がっていれば上書き更新になる
            Process.Start(ExtractVsix());
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("セットアップに失敗しました。\n\n" + ex.Message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    /// <summary>妨げになるプロセスがなくなるまで、閉じるよう案内します。</summary>
    /// <returns>すべて終了した場合は <see langword="true"/>、ユーザーがキャンセルした場合は <see langword="false"/>。</returns>
    private static bool WaitForBlockingProcesses()
    {
        while (true)
        {
            var running = BlockingProcesses.Where(p => IsRunning(p.ProcessName)).ToList();
            if (running.Count == 0)
            {
                return true;
            }

            // VS を閉じた後に残る MSBuild.exe は、VS がビルドに使った待機中のプロセスなので、ユーザーに確認して終了させる
            if (running.All(p => p.ProcessName == MSBuildProcessName))
            {
                if (MessageBox.Show(
                        "Visual Studio は閉じられていますが、Visual Studio がビルドに使った MSBuild.exe が残っているため、インストールを開始できません。\n\n"
                        + "OK を押すと MSBuild.exe を終了してインストールを続けます。\n"
                        + "(コマンドラインでビルド中の場合は、終わってから OK を押してください)",
                        Caption,
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning) != DialogResult.OK)
                {
                    return false;
                }

                KillAll(MSBuildProcessName);
                continue;
            }

            DialogResult answer = MessageBox.Show(
                "次のプロセスが起動しているため、インストールを開始できません。\n\n"
                + string.Join("\n", running.Select(p => "・" + p.DisplayName))
                + "\n\nすべて閉じてから OK を押してください。",
                Caption,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.OK)
            {
                return false;
            }
        }
    }

    private static bool IsRunning(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        foreach (Process process in processes)
        {
            process.Dispose();
        }

        return processes.Length != 0;
    }

    private static void KillAll(string processName)
    {
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // 既に終了している、または権限がない(管理者で実行された MSBuild など)。次の確認でもう一度案内する
                }
            }
        }
    }

    /// <summary>埋め込んだ .vsix を一時フォルダに書き出します。</summary>
    /// <returns>書き出した .vsix のパス。</returns>
    private static string ExtractVsix()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GitVisualizerSetup");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "GitVisualizer.vsix");

        using Stream resource = typeof(Program).Assembly.GetManifestResourceStream(VsixResourceName)
            ?? throw new InvalidOperationException(".vsix がセットアップに埋め込まれていません。");
        using FileStream file = File.Create(path);
        resource.CopyTo(file);
        return path;
    }
}
