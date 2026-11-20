using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GstInvoice.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GstInvoice.Core.Extraction;

/// <summary>
/// Google Gemini via its REST API. Its free tier (Flash models) makes it the zero-cost option
/// for a public demo. Same contract as <see cref="ClaudeInvoiceExtractor"/>: JSON constrained
/// by a schema, then validated by <see cref="Validation.InvoiceValidator"/>.
/// Note: on Gemini's free tier, Google may use inputs to improve its products, so only send
/// sample or masked invoices.
/// </summary>
public sealed class GeminiInvoiceExtractor(
    HttpClient http,
    IOptions<ExtractionOptions> options,
    ILogger<GeminiInvoiceExtractor> logger) : IInvoiceExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly HashSet<string> SupportedTypes =
        ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public async Task<ExtractionResult> ExtractAsync(byte[] content, string contentType, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.GeminiApiKey))
            throw new ExtractionException("GEMINI_API_KEY is not set.");
        if (contentType == "image/jpg") contentType = "image/jpeg";
        if (!SupportedTypes.Contains(contentType))
            throw new ExtractionException($"Unsupported file type '{contentType}'. Use PDF, JPG, PNG or WEBP.");

        var body = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["parts"] = new JsonArray(
                    new JsonObject
                    {
                        ["inline_data"] = new JsonObject
                        {
                            ["mime_type"] = contentType,
                            ["data"] = Convert.ToBase64String(content),
                        },
                    },
                    new JsonObject { ["text"] = ExtractionPrompt.Instructions }),
            }),
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = BuildSchema(),
                ["temperature"] = 0,
            },
        };

        var stopwatch = Stopwatch.StartNew();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{opts.GeminiModel}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("x-goog-api-key", opts.GeminiApiKey);

        using var response = await http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new ExtractionException("Gemini free-tier rate limit reached. Wait a minute and try again.");
        if (!response.IsSuccessStatusCode)
            throw new ExtractionException($"Gemini returned {(int)response.StatusCode}: {Truncate(raw)}");

        JsonNode? root;
        try { root = JsonNode.Parse(raw); }
        catch (JsonException ex) { throw new ExtractionException("Gemini returned a response that was not JSON.", ex); }

        var candidate = root?["candidates"]?[0];
        var finishReason = candidate?["finishReason"]?.GetValue<string>();
        if (finishReason is "SAFETY" or "RECITATION" or "PROHIBITED_CONTENT")
            throw new ExtractionException($"Gemini declined to process this file ({finishReason}).");
        if (finishReason == "MAX_TOKENS")
            throw new ExtractionException("Gemini ran out of output tokens before finishing.");

        var text = string.Concat(candidate?["content"]?["parts"]?.AsArray()
            .Select(p => p?["text"]?.GetValue<string>()) ?? []);
        if (string.IsNullOrWhiteSpace(text))
            throw new ExtractionException("Gemini returned no content.");

        ExtractedInvoice invoice;
        try
        {
            invoice = JsonSerializer.Deserialize<ExtractedInvoice>(text, JsonOptions)
                      ?? throw new ExtractionException("Gemini returned empty output.");
        }
        catch (JsonException ex)
        {
            throw new ExtractionException("Gemini output was not valid JSON.", ex);
        }

        stopwatch.Stop();
        var usage = root?["usageMetadata"];
        var inputTokens = usage?["promptTokenCount"]?.GetValue<long>() ?? 0;
        var outputTokens = usage?["candidatesTokenCount"]?.GetValue<long>() ?? 0;
        logger.LogInformation("Extracted invoice with {Model} in {Ms} ms ({In} in / {Out} out tokens)",
            opts.GeminiModel, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);

        return new ExtractionResult(ExtractionPrompt.Normalize(invoice), opts.GeminiModel, inputTokens, outputTokens,
            stopwatch.Elapsed);
    }

    /// <summary>Gemini's responseSchema uses an OpenAPI-style subset: uppercase types and "nullable".</summary>
    private static JsonObject BuildSchema()
    {
        JsonObject Field(string type) => new() { ["type"] = type, ["nullable"] = true };
        var properties = new JsonObject
        {
            ["SupplierName"] = Field("STRING"),
            ["SupplierGstin"] = Field("STRING"),
            ["BuyerGstin"] = Field("STRING"),
            ["InvoiceNumber"] = Field("STRING"),
            ["InvoiceDate"] = Field("STRING"),
            ["TaxableValue"] = Field("NUMBER"),
            ["Cgst"] = Field("NUMBER"),
            ["Sgst"] = Field("NUMBER"),
            ["Igst"] = Field("NUMBER"),
            ["Total"] = Field("NUMBER"),
        };
        var required = new JsonArray(properties.Select(p => (JsonNode)JsonValue.Create(p.Key)).ToArray());
        return new JsonObject { ["type"] = "OBJECT", ["properties"] = properties, ["required"] = required };
    }

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
