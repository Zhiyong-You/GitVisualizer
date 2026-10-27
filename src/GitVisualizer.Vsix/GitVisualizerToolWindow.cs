using System.Runtime.InteropServices;
using GitVisualizer.UI.ViewModels;
using GitVisualizer.UI.Views;
using Microsoft.VisualStudio.Shell;

namespace GitVisualizer.Vsix;

/// <summary>Git Visualizer のブランチ管理ツールウィンドウ。</summary>
[Guid(PackageIds.ToolWindowGuidString)]
public sealed class GitVisualizerToolWindow : ToolWindowPane
{
    /// <summary>ウィンドウのタイトル。</summary>
    public const string Title = "GitVisualizer ブランチ管理";

    /// <summary><see cref="GitVisualizerToolWindow"/> を初期化します(UI スレッドで呼ばれる)。</summary>
    /// <param name="viewModel"><see cref="GitVisualizerPackage"/>.InitializeToolWindowAsync で組み立てた ViewModel。</param>
    public GitVisualizerToolWindow(MainViewModel viewModel)
        : base(null)
    {
        Caption = Title;
        Content = new MainView(showWorkingChanges: false) { DataContext = viewModel };
    }
}
