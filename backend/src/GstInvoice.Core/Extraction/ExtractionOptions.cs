namespace GstInvoice.Core.Extraction;

public sealed class ExtractionOptions
{
    public const string Section = "Extraction";

    /// <summary>"Claude" or "Fake". Fake needs no API key and returns sample data.</summary>
    public string Provider { get; set; } = "Fake";

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>low | medium | high. Extraction is a reading task, so low is usually enough.</summary>
    public string Effort { get; set; } = "low";

    public int MaxTokens { get; set; } = 4000;
}
