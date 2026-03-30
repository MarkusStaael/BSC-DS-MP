using System.Text.Json;
using System.Text.Json.Serialization;

namespace BSC_DS_MP.TestHub;

internal class TestConfig
{
    [JsonPropertyName("solvers")]
    public string[] Solvers { get; set; } = [];

    [JsonPropertyName("files")]
    public string[]? Files { get; set; }

    [JsonPropertyName("dataDir")]
    public string DataDir { get; set; } = "Data";

    [JsonPropertyName("timeLimit")]
    public double TimeLimit { get; set; } = 10;

    [JsonPropertyName("maxNodes")]
    public int? MaxNodes { get; set; }

    [JsonPropertyName("metaCsv")]
    public string? MetaCsv { get; set; }

    [JsonPropertyName("csvOutput")]
    public string? CsvOutput { get; set; }

    [JsonPropertyName("printSolutions")]
    public bool PrintSolutions { get; set; } = false;

    [JsonPropertyName("verify")]
    public bool Verify { get; set; } = true;

    [JsonPropertyName("selfTest")]
    public bool SelfTest { get; set; } = false;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static TestConfig Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TestConfig>(json, _jsonOpts)
            ?? throw new Exception("Failed to deserialize config from " + path);
    }
}
