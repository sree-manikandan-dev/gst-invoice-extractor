// Runs the extractor over hand-labelled invoices and reports field-level accuracy.
//
//   dotnet run --project src/GstInvoice.Eval -- [dataDir] [--fake] [--min 0.85] [--out results.md]
//
// dataDir holds pairs: <name>.png|.jpg|.jpeg|.webp|.pdf + <name>.json (the correct values).
// Needs ANTHROPIC_API_KEY unless --fake is passed. Every real run costs money: ~10 invoices per run.
using System.Text;
using System.Text.Json;
using Anthropic;
using GstInvoice.Core.Evaluation;
using GstInvoice.Core.Extraction;
using GstInvoice.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var dataDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? FindDefaultDataDir();
var useFake = args.Contains("--fake");
var minAccuracy = ReadOption("--min") is { } m ? decimal.Parse(m) : (decimal?)null;
var outFile = ReadOption("--out");
var model = ReadOption("--model") ?? "claude-opus-5-5";

var mediaTypes = new Dictionary<string, string>
{
    [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
    [".webp"] = "image/webp", [".pdf"] = "application/pdf",
};

var cases = Directory.EnumerateFiles(dataDir, "*.json", SearchOption.AllDirectories)
    .Select(label => (label, file: mediaTypes.Keys.Select(ext => Path.ChangeExtension(label, ext)).FirstOrDefault(File.Exists)))
    .Where(c => c.file is not null)
    .OrderBy(c => c.label)
    .ToList();

if (cases.Count == 0)
{
    Console.Error.WriteLine($"No labelled invoices found in {dataDir}");
    return 1;
}

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning));
IInvoiceExtractor extractor = useFake
    ? new FakeInvoiceExtractor()
    : new ClaudeInvoiceExtractor(new AnthropicClient(),
        Options.Create(new ExtractionOptions { Provider = "Claude", Model = model }),
        loggerFactory.CreateLogger<ClaudeInvoiceExtractor>());

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var allResults = new List<FieldResult>();
var perInvoice = new List<(string Name, int Correct, int Total, string Wrong)>();
var latencies = new List<double>();
long inputTokens = 0, outputTokens = 0;
var failures = 0;

Console.WriteLine($"Evaluating {cases.Count} invoices from {dataDir} with {(useFake ? "fake extractor" : model)}\n");

foreach (var (labelPath, filePath) in cases)
{
    var name = Path.GetFileNameWithoutExtension(labelPath);
    var expected = JsonSerializer.Deserialize<ExtractedInvoice>(await File.ReadAllTextAsync(labelPath), jsonOptions)!;
    try
    {
        var result = await extractor.ExtractAsync(await File.ReadAllBytesAsync(filePath!), mediaTypes[Path.GetExtension(filePath!).ToLowerInvariant()]);
        var fields = FieldComparer.Compare(expected, result.Invoice);
        allResults.AddRange(fields);
        latencies.Add(result.Duration.TotalMilliseconds);
        inputTokens += result.InputTokens;
        outputTokens += result.OutputTokens;

        var wrong = string.Join(", ", fields.Where(f => !f.Correct).Select(f => $"{f.Field} (expected '{f.Expected}', got '{f.Actual}')"));
        perInvoice.Add((name, fields.Count(f => f.Correct), fields.Count, wrong));
        Console.WriteLine($"{name,-24} {fields.Count(f => f.Correct),2}/{fields.Count}  {wrong}");
    }
    catch (ExtractionException ex)
    {
        failures++;
        Console.WriteLine($"{name,-24} FAILED: {ex.Message}");
    }
}

if (allResults.Count == 0)
{
    Console.Error.WriteLine("Every extraction failed.");
    return 1;
}

var accuracy = (decimal)allResults.Count(r => r.Correct) / allResults.Count;
var byField = allResults.GroupBy(r => r.Field)
    .Select(g => (Field: g.Key, Accuracy: (decimal)g.Count(r => r.Correct) / g.Count()))
    .ToList();
var exactInvoices = perInvoice.Count(p => p.Correct == p.Total);

var report = new StringBuilder();
report.AppendLine($"# Extraction eval: {(useFake ? "fake" : model)}");
report.AppendLine();
report.AppendLine($"- Invoices: {cases.Count} ({failures} failed to extract)");
report.AppendLine($"- Field accuracy: **{accuracy:P1}** ({allResults.Count(r => r.Correct)}/{allResults.Count} fields)");
report.AppendLine($"- Invoices with every field correct: {exactInvoices}/{perInvoice.Count}");
if (latencies.Count > 0)
    report.AppendLine($"- Latency: median {Median(latencies) / 1000:0.0} s per invoice");
report.AppendLine($"- Tokens: {inputTokens:N0} in, {outputTokens:N0} out ({(perInvoice.Count > 0 ? (inputTokens + outputTokens) / perInvoice.Count : 0):N0} per invoice)");
report.AppendLine();
report.AppendLine("| Field | Accuracy |");
report.AppendLine("| --- | --- |");
foreach (var (field, acc) in byField) report.AppendLine($"| {field} | {acc:P0} |");
report.AppendLine();
report.AppendLine("| Invoice | Correct | Wrong fields |");
report.AppendLine("| --- | --- | --- |");
foreach (var p in perInvoice) report.AppendLine($"| {p.Name} | {p.Correct}/{p.Total} | {p.Wrong} |");

Console.WriteLine();
Console.WriteLine(report.ToString());
if (outFile is not null)
{
    await File.WriteAllTextAsync(outFile, report.ToString());
    Console.WriteLine($"Report written to {outFile}");
}

if (minAccuracy is not null && accuracy < minAccuracy)
{
    Console.Error.WriteLine($"Accuracy {accuracy:P1} is below the required {minAccuracy:P0}.");
    return 2;
}
return 0;

string? ReadOption(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static double Median(List<double> values)
{
    var sorted = values.Order().ToList();
    return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
}

static string FindDefaultDataDir()
{
    // Walk up from the current folder until we find the repo's eval-data folder.
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "eval-data");
        if (Directory.Exists(candidate)) return candidate;
    }
    return "eval-data";
}
