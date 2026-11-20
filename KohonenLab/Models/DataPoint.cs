namespace KohonenLab.Models;


public sealed class DataPoint
{
    public int Id { get; init; }
    public double X { get; init; }
    public double Y { get; init; }

    public DataPoint(int id, double x, double y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    public override string ToString() => $"{Id}: ({X:G}, {Y:G})";
}
