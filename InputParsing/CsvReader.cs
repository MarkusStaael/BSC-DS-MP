using System;
using System.Collections.Generic;
using System.IO;

namespace BSC_DS_MP.Reading;

public record CsvInstance(string Iid, int Nodes, int Edges, int BestScore);

public static class CsvReader
{
    public static List<CsvInstance> ReadInstances(string path)
    {
        Console.WriteLine("Reading CSV instances from file: " + path);
        var instances = new List<CsvInstance>();

        using (StreamReader reader = new StreamReader(path))
        {
            string line = reader.ReadLine(); // skip header
            if (line == null)
                throw new Exception("CSV file is empty");

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(',');
                if (parts.Length < 4)
                    throw new Exception("Invalid CSV line: " + line);

                var instance = new CsvInstance(
                    parts[0].Trim(),
                    int.Parse(parts[1].Trim()),
                    int.Parse(parts[2].Trim()),
                    int.Parse(parts[3].Trim())
                );

                instances.Add(instance);
            }
        }

        Console.WriteLine($"Read {instances.Count} instances");
        return instances;
    }
}
