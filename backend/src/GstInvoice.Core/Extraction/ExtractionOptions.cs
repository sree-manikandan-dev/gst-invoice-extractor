namespace GstInvoice.Core.Extraction;

public sealed class ExtractionOptions
{
    public const string Section = "Extraction";

    /// <summary>"Claude", "Gemini" or "Fake". Fake needs no API key and returns sample data.</summary>
    public string Provider { get; set; } = "Fake";

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>low | medium | high. Extraction is a reading task, so low is usually enough.</summary>
    public string Effort { get; set; } = "low";

    public int MaxTokens { get; set; } = 4000;

    /// <summary>Gemini model. "gemini-flash-latest" tracks Google's current free-tier Flash model.</summary>
    public string GeminiModel { get; set; } = "gemini-flash-latest";

    /// <summary>Read from the GEMINI_API_KEY environment variable at startup. Never put it in appsettings.</summary>
    public string? GeminiApiKey { get; set; }
}
