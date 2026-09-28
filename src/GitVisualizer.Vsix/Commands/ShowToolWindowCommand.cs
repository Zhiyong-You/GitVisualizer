using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace GitVisualizer.Vsix.Commands;

/// <summary>「表示 &gt; その他のウィンドウ &gt; Git Visualizer」コマンド。</summary>
internal static class ShowToolWindowCommand
{
    /// <summary>コマンドをメニューに登録します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    /// <returns>登録が終わると完了するタスク。</returns>
    public static async Task InitializeAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
            ?? throw new InvalidOperationException("IMenuCommandService を取得できませんでした。");

        var commandId = new CommandID(PackageIds.CommandSet, PackageIds.ShowToolWindowCommandId);
        commandService.AddCommand(new MenuCommand((_, _) => Execute(package), commandId));
    }

    private static void Execute(AsyncPackage package)
    {
        package.JoinableTaskFactory.RunAsync(async () =>
        {
            ToolWindowPane window = await package.ShowToolWindowAsync(
                typeof(GitVisualizerToolWindow),
                0,
                create: true,
                cancellationToken: package.DisposalToken);

            if (window?.Frame == null)
            {
                throw new NotSupportedException("Git Visualizer ウィンドウを作成できませんでした。");
            }
        }).FileAndForget("GitVisualizer/ShowToolWindow");
    }
}
