using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GitVisualizer.Core.Models;
using GitVisualizer.UI.ViewModels.Dialogs;

namespace GitVisualizer.UI.Views.Dialogs;

/// <summary>「履歴を表示」ウィンドウの View。</summary>
public partial class FileHistoryDialog : UserControl
{
    /// <summary><see cref="FileHistoryDialog"/> を初期化します。</summary>
    public FileHistoryDialog()
    {
        InitializeComponent();
    }

    private void OnEntryMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 項目の上でダブルクリックしたときだけ差分を開く(スクロールバーや余白は無視する)
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null && source is not ListBoxItem)
        {
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        if (source is ListBoxItem { DataContext: FileHistoryEntry entry } && DataContext is FileHistoryViewModel viewModel)
        {
            e.Handled = true;
            viewModel.OpenDiffCommand.Execute(entry);
        }
    }
}
