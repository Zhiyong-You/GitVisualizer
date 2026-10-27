using System.Linq;
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
        : this(false)
    {
    }

    /// <summary>ブランチ管理または変更ファイルの画面を初期化します。</summary>
    /// <param name="showWorkingChanges">変更ファイル画面の場合は <see langword="true"/>。</param>
    public MainView(bool showWorkingChanges)
    {
        InitializeComponent();
        // 共有 ViewModel に非表示側の選択イベントが届かないよう、使わないペインは取り外す。
        RootLayout.Children.Remove(showWorkingChanges ? BranchPane : WorkingChangesPane);
        if (showWorkingChanges)
        {
            RootLayout.Children.Remove(BranchToolbar);
            BranchRow.MinHeight = 0;
            BranchRow.Height = new GridLength(0);
            WorkingChangesRow.MinHeight = 120;
            WorkingChangesRow.Height = new GridLength(1, GridUnitType.Star);
            WorkingChangesToolbar.Visibility = Visibility.Visible;
        }
        else
        {
            RootLayout.Children.Remove(WorkingChangesToolbar);
        }

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

            // クリックしたブランチの履歴を、チェックアウトせずに表示する。
            // 再読み込みでブランチ一覧が作り直されたときは null で通知されるため、表示範囲は変わらない
            ViewModel.CommitHistory.ShowBranch(e.NewValue as BranchInfo);
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

    private void OnCommitFileMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 項目の上でダブルクリックしたときだけ差分を開く(スクロールバーや余白は無視する)
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: FileChange file })
        {
            e.Handled = true;
            OpenCommitFileDiff(file);
        }
    }

    private void OnCommitFileKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is ListBox { SelectedItem: FileChange file })
        {
            e.Handled = true;
            OpenCommitFileDiff(file);
        }
    }

    private void OnDiffSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // ListBox.SelectedItems はバインドできないため、選択が変わるたびに ViewModel へ渡す
        if (ViewModel != null && sender is ListBox list)
        {
            ViewModel.WorkingChanges.SelectedDiffRows = list.SelectedItems.OfType<DiffRowViewModel>().ToList();
        }
    }

    private void OnWorkingFileMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (e.ChangedButton == MouseButton.Left
            && FindAncestor<CheckBox>(source) == null
            && FindAncestor<ListBoxItem>(source) is { DataContext: FileChange file }
            && ViewModel?.WorkingChanges.OpenDiffCommand is { } command
            && command.CanExecute(file))
        {
            e.Handled = true;
            command.Execute(file);
        }
    }

    private void OnWorkingFileMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: FileChange } item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void OpenCommitFileDiff(FileChange file)
    {
        if (ViewModel?.CommitDetail.OpenDiffCommand is { } command && command.CanExecute(file))
        {
            command.Execute(file);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null && source is not T)
        {
            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return source as T;
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
