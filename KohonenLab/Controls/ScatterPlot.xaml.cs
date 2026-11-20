using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KohonenLab.Models;

namespace KohonenLab.Controls;


public partial class ScatterPlot : UserControl
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(PlotSeries), typeof(ScatterPlot),
        new PropertyMetadata(null, (s, _) => ((ScatterPlot)s).Redraw()));

    public PlotSeries? Series
    {
        get => (PlotSeries?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    private const double Left = 58, Right = 18, Top = 34, Bottom = 42;

    public ScatterPlot()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
    }

    private void Redraw()
    {
        PlotCanvas.Children.Clear();
        double width = PlotCanvas.ActualWidth;
        double height = PlotCanvas.ActualHeight;
        if (width < 80 || height < 80) return;

        if (Series is null || Series.Items.Count == 0)
        {
            AddText(12, height / 2, "Нет данных", 13, Brushes.Gray);
            return;
        }

        double xMin = Series.XMin ?? Series.Items.Min(i => i.X);
        double xMax = Series.XMax ?? Series.Items.Max(i => i.X);
        double yMin = Series.YMin ?? Series.Items.Min(i => i.Y);
        double yMax = Series.YMax ?? Series.Items.Max(i => i.Y);

        if (xMax - xMin < 1e-9) { xMin -= 1; xMax += 1; }
        if (yMax - yMin < 1e-9) { yMin -= 1; yMax += 1; }

        double px = (xMax - xMin) * 0.06;
        double py = (yMax - yMin) * 0.06;
        xMin -= px; xMax += px; yMin -= py; yMax += py;

        double plotW = width - Left - Right;
        double plotH = height - Top - Bottom;

        double X(double v) => Left + (v - xMin) / (xMax - xMin) * plotW;
        double Y(double v) => Top + plotH - (v - yMin) / (yMax - yMin) * plotH;

        
        var border = new Rectangle
        {
            Width = plotW, Height = plotH,
            Stroke = new SolidColorBrush(Color.FromRgb(200, 205, 212)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(border, Left);
        Canvas.SetTop(border, Top);
        PlotCanvas.Children.Add(border);

        for (int i = 0; i <= 4; i++)
        {
            double gx = Left + plotW * i / 4;
            double gy = Top + plotH * i / 4;
            AddLine(gx, Top, gx, Top + plotH, Color.FromRgb(236, 239, 243), 1);
            AddLine(Left, gy, Left + plotW, gy, Color.FromRgb(236, 239, 243), 1);

            double xv = xMin + (xMax - xMin) * i / 4;
            double yv = yMax - (yMax - yMin) * i / 4;
            AddText(gx - 18, Top + plotH + 6, Fmt(xv), 10, Brushes.DimGray);
            AddText(6, gy - 7, Fmt(yv), 10, Brushes.DimGray);
        }

        
        if (xMin < 0 && xMax > 0)
            AddLine(X(0), Top, X(0), Top + plotH, Color.FromRgb(150, 160, 175), 1);
        if (yMin < 0 && yMax > 0)
            AddLine(Left, Y(0), Left + plotW, Y(0), Color.FromRgb(150, 160, 175), 1);

        AddText(Left + plotW / 2 - 30, height - 16, Series.XLabel, 11, Brushes.DimGray);
        var yLabel = new TextBlock
        {
            Text = Series.YLabel, FontSize = 11, Foreground = Brushes.DimGray,
            RenderTransform = new RotateTransform(-90)
        };
        Canvas.SetLeft(yLabel, 2);
        Canvas.SetTop(yLabel, Top + plotH / 2 + 18);
        PlotCanvas.Children.Add(yLabel);

        AddText(Left + 4, 8, Series.Title, 12, Brushes.Black, FontWeights.SemiBold);

        
        if (Series.ConnectPoints)
        {
            foreach (var group in Series.Items.GroupBy(i => i.Brush.ToString()))
            {
                var points = group.ToList();
                for (int i = 1; i < points.Count; i++)
                {
                    if (points[i].X - points[i - 1].X > (xMax - xMin) * 0.2) continue;
                    AddLine(X(points[i - 1].X), Y(points[i - 1].Y),
                            X(points[i].X), Y(points[i].Y), Colors.SlateGray, 1.5);
                }
            }
        }

        
        foreach (PlotItem item in Series.Items.Where(i => i.DrawVectorFromOrigin))
        {
            double x0 = X(0), y0 = Y(0), x1 = X(item.X), y1 = Y(item.Y);
            var line = new Line
            {
                X1 = x0, Y1 = y0, X2 = x1, Y2 = y1,
                Stroke = item.Brush, StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            PlotCanvas.Children.Add(line);
        }

        
        int index = 0;
        foreach (PlotItem item in Series.Items)
        {
            double cx = X(item.X), cy = Y(item.Y);
            double r = item.Radius;

            if (item.DrawVectorFromOrigin)
            {
                var marker = new Rectangle
                {
                    Width = r * 2, Height = r * 2,
                    Fill = item.Brush, Stroke = Brushes.White, StrokeThickness = 1
                };
                Canvas.SetLeft(marker, cx - r);
                Canvas.SetTop(marker, cy - r);
                PlotCanvas.Children.Add(marker);
            }
            else
            {
                var ellipse = new Ellipse
                {
                    Width = r * 2, Height = r * 2,
                    Fill = item.Brush, Stroke = Brushes.White, StrokeThickness = 1
                };
                Canvas.SetLeft(ellipse, cx - r);
                Canvas.SetTop(ellipse, cy - r);
                PlotCanvas.Children.Add(ellipse);
            }

            if (!string.IsNullOrEmpty(item.Label) && Series.Items.Count <= 120)
                AddText(cx + r + 3, cy - 9, item.Label, 10, Brushes.Black);

            index++;
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Color color, double thickness)
    {
        PlotCanvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = new SolidColorBrush(color), StrokeThickness = thickness
        });
    }

    private void AddText(double x, double y, string text, double size, Brush brush, FontWeight? weight = null)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = size, Foreground = brush,
            FontWeight = weight ?? FontWeights.Normal
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        PlotCanvas.Children.Add(tb);
    }

    private static string Fmt(double v) =>
        Math.Abs(v) >= 100 ? v.ToString("F0", CultureInfo.InvariantCulture) :
        Math.Abs(v) >= 10 ? v.ToString("F1", CultureInfo.InvariantCulture) :
        v.ToString("F2", CultureInfo.InvariantCulture);
}
