namespace KohonenLab.Models;


public sealed class Neuron
{
    
    public double Wx { get; set; }
    public double Wy { get; set; }

    
    public double Fatigue { get; set; }

    
    public int Wins { get; set; }

    
    public int Index { get; init; }

    public Neuron(int index, double wx, double wy)
    {
        Index = index;
        Wx = wx;
        Wy = wy;
    }

    public double Norm => Math.Sqrt(Wx * Wx + Wy * Wy);

    public void Normalize()
    {
        double n = Norm;
        if (n < 1e-12)
        {
            Wx = 1;
            Wy = 0;
            return;
        }
        Wx /= n;
        Wy /= n;
    }
}
