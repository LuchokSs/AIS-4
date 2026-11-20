using System.Diagnostics;

namespace KohonenLab.Models;

public enum TrainingMode
{
    WinnerTakesAll,
    WithFatigue
}


public sealed class TrainingResult
{
    public TrainingMode Mode { get; init; }
    public IReadOnlyList<Neuron> Neurons { get; init; } = Array.Empty<Neuron>();
    public IReadOnlyList<double> ErrorHistory { get; init; } = Array.Empty<double>();
    public IReadOnlyList<int> Assignments { get; init; } = Array.Empty<int>();
    public IReadOnlyList<PointD> Centers { get; init; } = Array.Empty<PointD>();

    public double FinalError => ErrorHistory.Count == 0 ? 0 : ErrorHistory[^1];
    public int Epochs { get; init; }
    public long ElapsedMs { get; init; }
    public double ElapsedSeconds => ElapsedMs / 1000.0;
    public int DistinctWinners { get; init; }
}


public readonly record struct PointD(double X, double Y);
