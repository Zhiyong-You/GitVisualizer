using System.Windows;
using System.Windows.Media;
using GitVisualizer.Core.Graph;

namespace GitVisualizer.UI.Controls;

/// <summary>コミット履歴の 1 行分のグラフ(レーンの線とコミットの点)を描画します。</summary>
public sealed class CommitGraphCell : FrameworkElement
{
    /// <summary><see cref="Node"/> 依存関係プロパティ。</summary>
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node),
        typeof(GraphNode),
        typeof(CommitGraphCell),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>描画する行データ。</summary>
    public GraphNode? Node
    {
        get => (GraphNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        // TODO: Node.Edges の線と Node.Lane の点を描画する(X = レーン幅 × レーン番号、色は ColorIndex からパレットで決める)
    }
}
