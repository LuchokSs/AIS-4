using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using KohonenLab.Models;
using KohonenLab.Services;

namespace KohonenLab.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private string _dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "variant5.txt");
    private int _clusterCount = 3;
    private int _epochs = 1000;
    private double _initialAlpha = 0.5;
    private double _fatigueStep = 0.35;
    private double _fatigueDecay = 0.05;
    private int _seed = 42;
    private string _status = "Загрузите файл данных и нажмите «Обучить».";
    private string _estimatedClusters = "—";

    private PlotSeries _originalPlot = EmptySeries("Исходные данные");
    private PlotSeries _wtaPlot = EmptySeries("Классический WTA");
    private PlotSeries _fatiguePlot = EmptySeries("С утомляемостью");
    private PlotSeries _centersPlot = EmptySeries("Центры кластеров");
    private PlotSeries _errorPlot = EmptySeries("Кривая обучения");

    private KohonenTrainer? _trainer;
    private TrainingResult? _wtaResult;
    private TrainingResult? _fatigueResult;

    public MainViewModel()
    {
        LoadCommand = new RelayCommand(_ => LoadData());
        EstimateCommand = new RelayCommand(_ => EstimateClusters());
        TrainCommand = new RelayCommand(_ => Train());
        ExportCommand = new RelayCommand(_ => ExportResults());
    }

    public ICommand LoadCommand { get; }
    public ICommand EstimateCommand { get; }
    public ICommand TrainCommand { get; }
    public ICommand ExportCommand { get; }

    public ObservableCollection<PointRow> DataRows { get; } = new();
    public ObservableCollection<ComparisonRow> Comparison { get; } = new();

    public string DataPath { get => _dataPath; set => SetField(ref _dataPath, value); }
    public int ClusterCount { get => _clusterCount; set => SetField(ref _clusterCount, value); }
    public int Epochs { get => _epochs; set => SetField(ref _epochs, value); }
    public double InitialAlpha { get => _initialAlpha; set => SetField(ref _initialAlpha, value); }
    public double FatigueStep { get => _fatigueStep; set => SetField(ref _fatigueStep, value); }
    public double FatigueDecay { get => _fatigueDecay; set => SetField(ref _fatigueDecay, value); }
    public int Seed { get => _seed; set => SetField(ref _seed, value); }
    public string Status { get => _status; set => SetField(ref _status, value); }
    public string EstimatedClusters { get => _estimatedClusters; set => SetField(ref _estimatedClusters, value); }

    public PlotSeries OriginalPlot { get => _originalPlot; set => SetField(ref _originalPlot, value); }
    public PlotSeries WtaPlot { get => _wtaPlot; set => SetField(ref _wtaPlot, value); }
    public PlotSeries FatiguePlot { get => _fatiguePlot; set => SetField(ref _fatiguePlot, value); }
    public PlotSeries CentersPlot { get => _centersPlot; set => SetField(ref _centersPlot, value); }
    public PlotSeries ErrorPlot { get => _errorPlot; set => SetField(ref _errorPlot, value); }

    private void LoadData()
    {
        try
        {
            var points = DataLoader.Load(DataPath);
            _trainer = new KohonenTrainer(points);
            DataRows.Clear();

            foreach (DataPoint p in points)
            {
                PointD scaled = _trainer.ScaledPoints[p.Id - 1];
                DataRows.Add(new PointRow
                {
                    Id = p.Id,
                    X = p.X,
                    Y = p.Y,
                    NormalizedX = scaled.X,
                    NormalizedY = scaled.Y
                });
            }

            BuildOriginalPlot();
            BuildDataChart();
            Status = $"Загружено объектов: {points.Count}.";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка загрузки: {ex.Message}";
        }
    }

    private void EstimateClusters()
    {
        if (_trainer is null) { Status = "Сначала загрузите данные."; return; }
        int k = _trainer.EstimateClusterCount();
        EstimatedClusters = k.ToString();
        ClusterCount = k;
        Status = $"Оценка методом «локтя»: {k} кластер(ов). Число нейронов установлено равным {k}.";
    }

    private void Train()
    {
        if (_trainer is null) { Status = "Сначала загрузите данные."; return; }
        if (ClusterCount < 1) { Status = "Число нейронов должно быть не меньше 1."; return; }

        try
        {
            _wtaResult = _trainer.Train(TrainingMode.WinnerTakesAll, ClusterCount, Epochs, InitialAlpha, seed: Seed);
            _fatigueResult = _trainer.Train(TrainingMode.WithFatigue, ClusterCount, Epochs, InitialAlpha,
                FatigueStep, FatigueDecay, Seed);

            ApplyAssignments(_wtaResult, isWta: true);
            ApplyAssignments(_fatigueResult, isWta: false);

            WtaPlot = BuildTrainingPlot(_wtaResult, "Классический WTA", Colors.SteelBlue);
            FatiguePlot = BuildTrainingPlot(_fatigueResult, "С утомляемостью", Colors.DarkOrange);
            CentersPlot = BuildCentersPlot();
            ErrorPlot = BuildErrorPlot();
            BuildComparison();
            BuildOriginalPlot();

            Status = "Обучение завершено. Результаты можно экспортировать в JSON.";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка обучения: {ex.Message}";
        }
    }

    private void BuildOriginalPlot()
    {
        if (_trainer is null) return;

        var items = new List<PlotItem>();
        foreach (DataPoint p in _trainer.Points)
        {
            Brush brush = _wtaResult is null
                ? Brushes.SteelBlue
                : ClusterColors[_wtaResult.Assignments[p.Id - 1] % ClusterColors.Length];
            items.Add(new PlotItem
            {
                X = p.X, Y = p.Y, Brush = brush, Radius = 6, Label = p.Id.ToString()
            });
        }

        OriginalPlot = new PlotSeries
        {
            Title = "Объекты в исходном пространстве признаков",
            Items = items,
            XLabel = "Признак X",
            YLabel = "Признак Y",
            XMin = _trainer.Points.Min(p => p.X),
            XMax = _trainer.Points.Max(p => p.X),
            YMin = _trainer.Points.Min(p => p.Y),
            YMax = _trainer.Points.Max(p => p.Y)
        };
    }

    private void BuildDataChart()
    {
        if (_trainer is null) return;

        var items = _trainer.ScaledPoints
            .Select((p, i) => new PlotItem
            {
                X = p.X, Y = p.Y, Brush = Brushes.SteelBlue, Radius = 5, Label = (i + 1).ToString()
            })
            .ToList();

        WtaPlot = new PlotSeries
        {
            Title = "Нормированные объекты (min-max)",
            Items = items,
            XLabel = "x'",
            YLabel = "y'"
        };
    }

    private PlotSeries BuildTrainingPlot(TrainingResult result, string title, Color neuronColor)
    {
        var items = new List<PlotItem>();

        foreach (PointD p in _trainer!.UnitPoints)
            items.Add(new PlotItem { X = p.X, Y = p.Y, Brush = Brushes.SteelBlue, Radius = 4 });

        foreach (Neuron n in result.Neurons)
        {
            items.Add(new PlotItem
            {
                X = n.Wx, Y = n.Wy,
                Brush = new SolidColorBrush(neuronColor),
                Radius = 7,
                Label = $"W{n.Index + 1}",
                DrawVectorFromOrigin = true
            });
        }

        return new PlotSeries
        {
            Title = title,
            Items = items,
            XLabel = "нормированный x",
            YLabel = "нормированный y",
            XMin = -1.1, XMax = 1.1, YMin = -1.1, YMax = 1.1
        };
    }

    private PlotSeries BuildCentersPlot()
    {
        var items = new List<PlotItem>();
        foreach (DataPoint p in _trainer!.Points)
        {
            int c = _wtaResult!.Assignments[p.Id - 1];
            items.Add(new PlotItem
            {
                X = p.X, Y = p.Y,
                Brush = ClusterColors[c % ClusterColors.Length],
                Radius = 5,
                Label = p.Id.ToString()
            });
        }

        foreach (PointD center in _wtaResult!.Centers)
        {
            items.Add(new PlotItem
            {
                X = center.X, Y = center.Y,
                Brush = Brushes.Crimson, Radius = 10, Label = "C"
            });
        }

        return new PlotSeries
        {
            Title = "Масштабированные веса — центры кластеров",
            Items = items,
            XLabel = "Признак X",
            YLabel = "Признак Y",
            XMin = _trainer.Points.Min(p => p.X),
            XMax = _trainer.Points.Max(p => p.X),
            YMin = _trainer.Points.Min(p => p.Y),
            YMax = _trainer.Points.Max(p => p.Y)
        };
    }

    private PlotSeries BuildErrorPlot()
    {
        var items = new List<PlotItem>();
        if (_wtaResult is not null)
            items.AddRange(ErrorItems(_wtaResult.ErrorHistory, Colors.SteelBlue));
        if (_fatigueResult is not null)
            items.AddRange(ErrorItems(_fatigueResult.ErrorHistory, Colors.DarkOrange));

        return new PlotSeries
        {
            Title = "Количественная ошибка квантования",
            Items = items,
            XLabel = "Эпоха",
            YLabel = "E",
            ConnectPoints = true,
            XMin = 0, XMax = Epochs
        };
    }

    private static IEnumerable<PlotItem> ErrorItems(IReadOnlyList<double> errors, Color color)
    {
        double max = errors.Count == 0 ? 1 : errors.Max();
        return errors.Select((e, i) => new PlotItem
        {
            X = i,
            Y = max < 1e-9 ? 0 : e / max,
            Brush = new SolidColorBrush(color),
            Radius = 2
        });
    }

    private void ApplyAssignments(TrainingResult result, bool isWta)
    {
        for (int i = 0; i < DataRows.Count && i < result.Assignments.Count; i++)
        {
            if (isWta) DataRows[i].WtaCluster = result.Assignments[i] + 1;
            else DataRows[i].FatigueCluster = result.Assignments[i] + 1;
        }
    }

    private void BuildComparison()
    {
        Comparison.Clear();
        if (_wtaResult is null || _fatigueResult is null) return;

        Comparison.Add(new ComparisonRow
        {
            Indicator = "Число эпох",
            Wta = _wtaResult.Epochs.ToString(),
            Fatigue = _fatigueResult.Epochs.ToString()
        });
        Comparison.Add(new ComparisonRow
        {
            Indicator = "Время обучения, мс",
            Wta = _wtaResult.ElapsedMs.ToString(),
            Fatigue = _fatigueResult.ElapsedMs.ToString()
        });
        Comparison.Add(new ComparisonRow
        {
            Indicator = "Ошибка квантования E",
            Wta = _wtaResult.FinalError.ToString("F5"),
            Fatigue = _fatigueResult.FinalError.ToString("F5")
        });
        Comparison.Add(new ComparisonRow
        {
            Indicator = "Использовано нейронов",
            Wta = _wtaResult.DistinctWinners.ToString(),
            Fatigue = _fatigueResult.DistinctWinners.ToString()
        });
    }

    private void ExportResults()
    {
        if (_trainer is null || _wtaResult is null || _fatigueResult is null)
        {
            Status = "Сначала загрузите данные и проведите обучение.";
            return;
        }

        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "variant5_results.json");

            using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            writer.WriteLine("{");
            writer.WriteLine("  \"points\": [");
            for (int i = 0; i < _trainer.Points.Count; i++)
            {
                DataPoint p = _trainer.Points[i];
                PointD s = _trainer.ScaledPoints[i];
                writer.Write("    {\"id\":" + p.Id +
                             ",\"x\":" + Inv(p.X) +
                             ",\"y\":" + Inv(p.Y) +
                             ",\"nx\":" + Inv(s.X) +
                             ",\"ny\":" + Inv(s.Y) +
                             ",\"wta\":" + (_wtaResult.Assignments[i] + 1) +
                             ",\"fatigue\":" + (_fatigueResult.Assignments[i] + 1) + "}");
                writer.WriteLine(i == _trainer.Points.Count - 1 ? "" : ",");
            }
            writer.WriteLine("  ],");
            WriteResult(writer, "wta", _wtaResult, isLast: false);
            WriteResult(writer, "fatigue", _fatigueResult, isLast: true);
            writer.WriteLine("}");

            Status = $"Результаты сохранены: {path}";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка экспорта: {ex.Message}";
        }
    }

    private static string Inv(double v) =>
        v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);

    private static void WriteResult(StreamWriter w, string name, TrainingResult r, bool isLast)
    {
        w.WriteLine($"  \"{name}\": {{");
        w.WriteLine($"    \"epochs\": {r.Epochs},");
        w.WriteLine($"    \"elapsedMs\": {r.ElapsedMs},");
        w.WriteLine($"    \"finalError\": {Inv(r.FinalError)},");
        w.WriteLine($"    \"distinctWinners\": {r.DistinctWinners},");
        w.WriteLine("    \"weights\": [" + string.Join(",", r.Neurons.Select(n => $"{{\"x\":{Inv(n.Wx)},\"y\":{Inv(n.Wy)},\"wins\":{n.Wins}}}")) + "],");
        w.WriteLine("    \"centers\": [" + string.Join(",", r.Centers.Select(c => $"{{\"x\":{Inv(c.X)},\"y\":{Inv(c.Y)}}}")) + "],");
        w.WriteLine("    \"errorHistory\": [" + string.Join(",", r.ErrorHistory.Select(Inv)) + "]");
        w.WriteLine("  }" + (isLast ? "" : ","));
    }

    private static PlotSeries EmptySeries(string title) => new()
    {
        Title = title,
        Items = Array.Empty<PlotItem>()
    };

    internal static readonly SolidColorBrush[] ClusterColors =
    {
        new(Colors.SteelBlue), new(Colors.SeaGreen), new(Colors.DarkOrange),
        new(Colors.MediumPurple), new(Colors.Crimson), new(Colors.Goldenrod)
    };
}
