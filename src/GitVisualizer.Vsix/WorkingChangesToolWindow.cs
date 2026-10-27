using System.Runtime.InteropServices;
using GitVisualizer.UI.ViewModels;
using GitVisualizer.UI.Views;
using Microsoft.VisualStudio.Shell;

namespace GitVisualizer.Vsix;

/// <summary>Git Visualizer の変更ファイルツールウィンドウ。</summary>
[Guid(PackageIds.WorkingChangesToolWindowGuidString)]
public sealed class WorkingChangesToolWindow : ToolWindowPane
{
    /// <summary>ウィンドウのタイトル。</summary>
    public const string Title = "GitVisualizer 変更ファイル";

    /// <summary>変更ファイル画面を初期化します。</summary>
    /// <param name="viewModel">ブランチ管理画面と共有する ViewModel。</param>
    public WorkingChangesToolWindow(MainViewModel viewModel)
        : base(null)
    {
        Caption = Title;
        Content = new MainView(showWorkingChanges: true) { DataContext = viewModel };
    }
}
