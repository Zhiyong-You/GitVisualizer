using System.Windows;
using System.Windows.Media;
using GitVisualizer.Core.Graph;

namespace GitVisualizer.UI.Controls;

/// <summary>コミットグラフの描画寸法。</summary>
public static class CommitGraphMetrics
{
    /// <summary>1 レーンの幅(px)。</summary>
    public const double LaneWidth = 12;

    /// <summary>コミットの点の半径(px)。</summary>
    public const double DotRadius = 3.5;

    /// <summary>線の太さ(px)。</summary>
    public const double LineThickness = 1.6;
}

/// <summary>コミット履歴の 1 行分のグラフ(レーンの線とコミットの点)を描画します。</summary>
public sealed class CommitGraphCell : FrameworkElement
{
    /// <summary><see cref="Node"/> 依存関係プロパティ。</summary>
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node),
        typeof(GraphNode),
        typeof(CommitGraphCell),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary><see cref="Highlight"/> 依存関係プロパティ。</summary>
    public static readonly DependencyProperty HighlightProperty = DependencyProperty.Register(
        nameof(Highlight),
        typeof(GraphHighlight),
        typeof(CommitGraphCell),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // 強調表示中に、経路以外の線と点を薄く描く不透明度
    private const double DimmedOpacity = 0.25;

    // レーンの色。VS テーマのリソースには系列色がないため、ダーク/ライトどちらの背景でも判別しやすい中間の明度の色を使う
    private static readonly Brush[] Palette = CreatePalette(
        Color.FromRgb(0x3E, 0x9B, 0xE0),
        Color.FromRgb(0xE0, 0x7B, 0x2E),
        Color.FromRgb(0x4C, 0xB0, 0x50),
        Color.FromRgb(0xC5, 0x4F, 0xC9),
        Color.FromRgb(0xD9, 0x4F, 0x4F),
        Color.FromRgb(0x26, 0xA6, 0x9A),
        Color.FromRgb(0xB8, 0x9B, 0x2A),
        Color.FromRgb(0x7E, 0x7E, 0xE6));

    private static readonly Brush[] DimmedPalette = CreateDimmedPalette();

    private static readonly Pen[] Pens = CreatePens(Palette);

    private static readonly Pen[] DimmedPens = CreatePens(DimmedPalette);

    /// <summary><see cref="CommitGraphCell"/> を初期化します。</summary>
    public CommitGraphCell()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    /// <summary>描画する行データ。</summary>
    public GraphNode? Node
    {
        get => (GraphNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <summary>強調表示する線と点。<see langword="null"/> なら全て通常の色で描く。</summary>
    public GraphHighlight? Highlight
    {
        get => (GraphHighlight?)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        GraphNode? node = Node;
        if (node == null)
        {
            return;
        }

        double height = ActualHeight;
        double middle = height / 2;
        var dot = new Point(X(node.Lane), middle);
        GraphHighlight? highlight = Highlight;

        // 線: 素通りは上端→下端、子からの線は上端→点、親への線は点→下端
        foreach (GraphEdge edge in node.Edges)
        {
            Pen[] pens = highlight == null || highlight.Contains(edge) ? Pens : DimmedPens;
            Pen pen = pens[edge.ColorIndex % pens.Length];
            switch (edge.Kind)
            {
                case GraphEdgeKind.Incoming:
                    DrawCurve(drawingContext, pen, new Point(X(edge.FromLane), 0), dot);
                    break;
                case GraphEdgeKind.Outgoing:
                    DrawCurve(drawingContext, pen, dot, new Point(X(edge.ToLane), height));
                    break;
                default:
                    DrawCurve(drawingContext, pen, new Point(X(edge.FromLane), 0), new Point(X(edge.ToLane), height));
                    break;
            }
        }

        // コミットの点
        Brush[] palette = highlight == null || highlight.Contains(node) ? Palette : DimmedPalette;
        drawingContext.DrawEllipse(palette[node.ColorIndex % palette.Length], null, dot, CommitGraphMetrics.DotRadius, CommitGraphMetrics.DotRadius);
    }

    private static double X(int lane) => (CommitGraphMetrics.LaneWidth * lane) + (CommitGraphMetrics.LaneWidth / 2) + 2;

    private static void DrawCurve(DrawingContext drawingContext, Pen pen, Point from, Point to)
    {
        if (from.X == to.X)
        {
            drawingContext.DrawLine(pen, from, to);
            return;
        }

        // レーンをまたぐ線は S 字の曲線にする
        double controlY = (from.Y + to.Y) / 2;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(from, isFilled: false, isClosed: false);
            context.BezierTo(new Point(from.X, controlY), new Point(to.X, controlY), to, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private static Brush[] CreatePalette(params Color[] colors)
    {
        var brushes = new Brush[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            var brush = new SolidColorBrush(colors[i]);
            brush.Freeze();
            brushes[i] = brush;
        }

        return brushes;
    }

    private static Brush[] CreateDimmedPalette()
    {
        var brushes = new Brush[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
        {
            Brush brush = Palette[i].CloneCurrentValue();
            brush.Opacity = DimmedOpacity;
            brush.Freeze();
            brushes[i] = brush;
        }

        return brushes;
    }

    private static Pen[] CreatePens(Brush[] palette)
    {
        var pens = new Pen[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            var pen = new Pen(palette[i], CommitGraphMetrics.LineThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            pens[i] = pen;
        }

        return pens;
    }
}
