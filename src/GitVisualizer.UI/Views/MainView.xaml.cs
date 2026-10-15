using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GitVisualizer.Core.Models;
using GitVisualizer.UI.ViewModels;

namespace GitVisualizer.UI.Views;

/// <summary>Git Visualizer ツールウィンドウのメイン画面。</summary>
public partial class MainView : UserControl
{
    private bool _loadedOnce;

    /// <summary><see cref="MainView"/> を初期化します。</summary>
    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 初めて表示されたときにリポジトリを読み込む(以降はファイル監視やソリューションの切り替えで自動更新)
        if (_loadedOnce || ViewModel == null)
        {
            return;
        }

        _loadedOnce = true;
        ViewModel.RefreshCommand.Execute(null);
    }

    private void OnBranchTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel != null)
        {
            // グループ見出し(ローカル / リモート)を選んだ場合は null になる
            ViewModel.BranchTree.SelectedBranch = e.NewValue as BranchInfo;
        }
    }

    private void OnBranchTreeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // ブランチの行をダブルクリックしたときだけチェックアウトする(グループ見出しは展開/折りたたみ)
        if (ViewModel?.BranchTree is not { SelectedBranch: BranchInfo } branchTree || !IsOnBranchItem(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        if (branchTree.CheckoutSelectedCommand.CanExecute(null))
        {
            branchTree.CheckoutSelectedCommand.Execute(null);
        }
    }

    private static bool IsOnBranchItem(DependencyObject? source)
    {
        while (source != null && source is not TreeView)
        {
            if (source is TreeViewItem item)
            {
                return item.DataContext is BranchInfo && item.Header is BranchInfo;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }
}
