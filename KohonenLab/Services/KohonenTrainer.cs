using System.Diagnostics;
using KohonenLab.Models;

namespace KohonenLab.Services;






public sealed class KohonenTrainer
{
    private readonly IReadOnlyList<DataPoint> _points;
    private readonly double _minX, _maxX, _minY, _maxY;
    private readonly List<PointD> _scaled = new();
    private readonly List<PointD> _unit = new();

    public IReadOnlyList<DataPoint> Points => _points;
    public IReadOnlyList<PointD> ScaledPoints => _scaled;
    public IReadOnlyList<PointD> UnitPoints => _unit;

    public KohonenTrainer(IEnumerable<DataPoint> points)
    {
        _points = points.ToList();
        _minX = _points.Min(p => p.X);
        _maxX = _points.Max(p => p.X);
        _minY = _points.Min(p => p.Y);
        _maxY = _points.Max(p => p.Y);

        double spanX = Math.Max(_maxX - _minX, 1e-12);
        double spanY = Math.Max(_maxY - _minY, 1e-12);

        foreach (DataPoint p in _points)
        {
            double sx = (p.X - _minX) / spanX;
            double sy = (p.Y - _minY) / spanY;
            _scaled.Add(new PointD(sx, sy));

            double len = Math.Sqrt(sx * sx + sy * sy);
            _unit.Add(len < 1e-12 ? new PointD(1, 0) : new PointD(sx / len, sy / len));
        }
    }

    
    public PointD Unscale(PointD p) => new(
        _minX + p.X * (_maxX - _minX),
        _minY + p.Y * (_maxY - _minY));

    
    
    
    
    public TrainingResult Train(
        TrainingMode mode,
        int neuronCount,
        int epochs,
        double initialAlpha,
        double fatigueStep = 0.35,
        double fatigueDecay = 0.08,
        int seed = 42,
        double fatiguePenalty = 0.5)
    {
        if (neuronCount < 1) throw new ArgumentOutOfRangeException(nameof(neuronCount));
        if (epochs < 1) throw new ArgumentOutOfRangeException(nameof(epochs));

        var random = new Random(seed);
        
        
        
        var neurons = new List<Neuron>(neuronCount);
        var chosen = new List<PointD>();
        for (int i = 0; i < neuronCount; i++)
        {
            PointD sample;
            if (chosen.Count == 0)
            {
                sample = _unit[random.Next(_unit.Count)];
            }
            else
            {
                var weights = _unit.Select(p => chosen.Min(c => Dist2(p, c))).ToList();
                double total = weights.Sum();
                double r = random.NextDouble() * total;
                int index = 0;
                while (index < weights.Count - 1 && r > weights[index])
                {
                    r -= weights[index];
                    index++;
                }
                sample = _unit[index];
            }

            chosen.Add(sample);
            neurons.Add(new Neuron(i,
                sample.X + (random.NextDouble() - 0.5) * 0.02,
                sample.Y + (random.NextDouble() - 0.5) * 0.02));
        }
        foreach (Neuron n in neurons) n.Normalize();

        var errors = new List<double>(epochs);
        var sw = Stopwatch.StartNew();

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            
            double progress = epoch / (double)Math.Max(1, epochs - 1);
            double alpha = Math.Max(0.005, initialAlpha * Math.Pow(1 - progress, 1.4));

            foreach (PointD input in _unit)
            {
                if (mode == TrainingMode.WithFatigue)
                {
                    foreach (Neuron n in neurons)
                        n.Fatigue = Math.Max(0, n.Fatigue * (1 - fatigueDecay));
                }

                Neuron bmu = FindWinner(neurons, input, mode == TrainingMode.WithFatigue, fatiguePenalty);

                bmu.Wx += alpha * (input.X - bmu.Wx);
                bmu.Wy += alpha * (input.Y - bmu.Wy);
                bmu.Normalize();
                bmu.Wins++;

                if (mode == TrainingMode.WithFatigue)
                    bmu.Fatigue = Math.Min(2.0, bmu.Fatigue + fatigueStep);
            }

            errors.Add(QuantizationError(neurons));
        }

        sw.Stop();

        
        var assignments = new List<int>(_unit.Count);
        foreach (PointD input in _unit)
            assignments.Add(FindWinner(neurons, input, useFatigue: false).Index);

        var centers = new List<PointD>(neuronCount);
        for (int i = 0; i < neuronCount; i++)
        {
            Neuron n = neurons[i];
            var assigned = _scaled.Where((_, idx) => assignments[idx] == i).ToList();

            double radius;
            if (assigned.Count > 0)
            {
                
                
                radius = assigned.Average(p => p.X * n.Wx + p.Y * n.Wy);
            }
            else
            {
                radius = _scaled.Average(p => Math.Sqrt(p.X * p.X + p.Y * p.Y)) * 0.7;
            }

            centers.Add(Unscale(new PointD(n.Wx * radius, n.Wy * radius)));
        }

        return new TrainingResult
        {
            Mode = mode,
            Neurons = neurons,
            ErrorHistory = errors,
            Assignments = assignments,
            Centers = centers,
            Epochs = epochs,
            ElapsedMs = sw.ElapsedMilliseconds,
            DistinctWinners = assignments.Distinct().Count()
        };
    }

    private static Neuron FindWinner(IReadOnlyList<Neuron> neurons, PointD input, bool useFatigue, double fatiguePenalty = 0.5)
    {
        Neuron best = neurons[0];
        double bestScore = double.MaxValue;

        foreach (Neuron n in neurons)
        {
            double dx = input.X - n.Wx;
            double dy = input.Y - n.Wy;
            double distance2 = dx * dx + dy * dy;

            
            
            double score = useFatigue ? distance2 + fatiguePenalty * n.Fatigue : distance2;

            if (score < bestScore)
            {
                bestScore = score;
                best = n;
            }
        }

        return best;
    }

    private static double Dist2(PointD a, PointD b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private double QuantizationError(IReadOnlyList<Neuron> neurons)
    {
        double sum = 0;
        foreach (PointD input in _unit)
        {
            double min = neurons.Min(n =>
            {
                double dx = input.X - n.Wx;
                double dy = input.Y - n.Wy;
                return dx * dx + dy * dy;
            });
            sum += Math.Sqrt(min);
        }
        return sum / _unit.Count;
    }

    
    
    
    
    public int EstimateClusterCount(int maxK = 6)
    {
        var inertias = new List<double>();
        for (int k = 2; k <= Math.Min(maxK, _scaled.Count - 1); k++)
            inertias.Add(KMeansInertia(k));

        if (inertias.Count == 0) return 1;

        int bestK = 2;
        double bestGain = 0;
        for (int i = 1; i < inertias.Count; i++)
        {
            double gain = (inertias[i - 1] - inertias[i]) / Math.Max(inertias[i - 1], 1e-9);
            if (gain > bestGain)
            {
                bestGain = gain;
                bestK = i + 2;
            }
        }

        return bestGain < 0.12 ? 2 : bestK;
    }

    private double KMeansInertia(int k)
    {
        var random = new Random(123 + k);
        var centers = _scaled.OrderBy(_ => random.Next()).Take(k).ToList();

        for (int iter = 0; iter < 50; iter++)
        {
            var groups = Enumerable.Range(0, k).Select(_ => new List<PointD>()).ToList();
            foreach (PointD p in _scaled)
            {
                int best = 0;
                double bestD = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double dx = p.X - centers[c].X;
                    double dy = p.Y - centers[c].Y;
                    double d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = c; }
                }
                groups[best].Add(p);
            }

            for (int c = 0; c < k; c++)
            {
                if (groups[c].Count == 0) continue;
                centers[c] = new PointD(groups[c].Average(p => p.X), groups[c].Average(p => p.Y));
            }
        }

        double inertia = 0;
        foreach (PointD p in _scaled)
        {
            double min = centers.Min(c =>
            {
                double dx = p.X - c.X;
                double dy = p.Y - c.Y;
                return dx * dx + dy * dy;
            });
            inertia += min;
        }
        return inertia;
    }
}
