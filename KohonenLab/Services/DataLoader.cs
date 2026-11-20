using System.Globalization;
using System.IO;
using KohonenLab.Models;

namespace KohonenLab.Services;


public static class DataLoader
{
    
    
    
    
    public static List<DataPoint> Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл данных не найден: {path}", path);

        var result = new List<DataPoint>();
        int id = 0;

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//"))
                continue;

            string normalized = line
                .Replace(',', ' ')
                .Replace(';', ' ')
                .Replace('\t', ' ');

            string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            
            int start = 0;
            if (parts.Length >= 3 && int.TryParse(parts[0], out _))
                start = 1;

            if (!double.TryParse(parts[start], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(parts[start + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                continue;

            result.Add(new DataPoint(++id, x, y));
        }

        if (result.Count < 2)
            throw new InvalidDataException("В файле данных меньше двух корректных строк.");

        return result;
    }
}
