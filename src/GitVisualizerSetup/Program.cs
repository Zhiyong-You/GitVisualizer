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

    // VSIXInstaller が終了を待つプロセス(MSBuild は VS を閉じた後もしばらく残ることがある)
    private static readonly (string ProcessName, string DisplayName)[] BlockingProcesses =
    {
        ("devenv", "Visual Studio (devenv.exe)"),
        ("MSBuild", "MSBuild.exe"),
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
            string[] running = BlockingProcesses.Where(p => IsRunning(p.ProcessName)).Select(p => p.DisplayName).ToArray();
            if (running.Length == 0)
            {
                return true;
            }

            DialogResult answer = MessageBox.Show(
                "次のプロセスが起動しているため、インストールを開始できません。\n\n"
                + string.Join("\n", running.Select(name => "・" + name))
                + "\n\nすべて閉じてから OK を押してください。\n"
                + "(Visual Studio を閉じた後も MSBuild.exe が残っている場合は、しばらく待つか、タスク マネージャーで終了してください)",
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
