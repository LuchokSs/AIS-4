namespace KohonenLab.Models;

using System.Windows.Media;


public sealed class PlotItem
{
    public double X { get; init; }
    public double Y { get; init; }
    public Brush Brush { get; init; } = Brushes.SteelBlue;
    public double Radius { get; init; } = 4;
    public string? Label { get; init; }
    public bool DrawVectorFromOrigin { get; init; }
}


public sealed class PlotSeries
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<PlotItem> Items { get; init; } = Array.Empty<PlotItem>();
    public string XLabel { get; init; } = "X";
    public string YLabel { get; init; } = "Y";
    public bool ConnectPoints { get; init; }

    public double? XMin { get; init; }
    public double? XMax { get; init; }
    public double? YMin { get; init; }
    public double? YMax { get; init; }
}
