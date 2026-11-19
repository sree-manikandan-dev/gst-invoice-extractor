using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using GstInvoice.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GstInvoice.Core.Extraction;

/// <summary>
/// Sends the invoice (image or PDF) to Claude and asks for JSON that matches a strict schema
/// (structured outputs). The schema guarantees parseable output; it does NOT guarantee correct
/// values, which is why <see cref="Validation.InvoiceValidator"/> runs afterwards.
/// </summary>
public sealed class ClaudeInvoiceExtractor(
    AnthropicClient client,
    IOptions<ExtractionOptions> options,
    ILogger<ClaudeInvoiceExtractor> logger) : IInvoiceExtractor
{
    private const string Instructions = """
        You are reading one Indian GST purchase invoice. Extract the fields into the JSON schema.
        Rules:
        - Copy GSTINs exactly as printed (15 characters, uppercase). Do not guess characters you cannot read.
        - InvoiceDate must be yyyy-MM-dd. Indian bills usually print dd/mm/yyyy.
        - Amounts are plain numbers in rupees, no commas or currency symbols.
        - TaxableValue is the total before tax. Total is the final invoice amount.
        - If a field is not on the invoice or is unreadable, use null. Never invent values.
        - BuyerGstin is the recipient's GSTIN ("Bill to"), if printed.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ExtractionResult> ExtractAsync(byte[] content, string contentType, CancellationToken ct = default)
    {
        var opts = options.Value;
        var stopwatch = Stopwatch.StartNew();

        var parameters = new MessageCreateParams
        {
            Model = opts.Model,
            MaxTokens = opts.MaxTokens,
            OutputConfig = new OutputConfig
            {
                Effort = ParseEffort(opts.Effort),
                Format = new JsonOutputFormat { Schema = BuildSchema() },
            },
            Messages =
            [
                new UserMessageParam
                {
                    Content = new List<ContentBlockParam> { BuildFileBlock(content, contentType), new TextBlockParam { Text = Instructions } },
                },
            ],
        };

        Message response;
        try
        {
            response = await client.Messages.Create(parameters, ct);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new ExtractionException("The AI provider is rate limiting requests. Try again shortly.", ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new ExtractionException($"The AI provider returned an error: {ex.Message}", ex);
        }

        if (response.StopReason == "refusal")
            throw new ExtractionException("The model declined to process this file.");
        if (response.StopReason == "max_tokens")
            throw new ExtractionException("The model ran out of output tokens before finishing.");

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        ExtractedInvoice invoice;
        try
        {
            invoice = JsonSerializer.Deserialize<ExtractedInvoice>(json, JsonOptions)
                      ?? throw new ExtractionException("The model returned empty output.");
        }
        catch (JsonException ex)
        {
            throw new ExtractionException("The model output was not valid JSON.", ex);
        }

        stopwatch.Stop();
        logger.LogInformation("Extracted invoice with {Model} in {Ms} ms ({In} in / {Out} out tokens)",
            opts.Model, stopwatch.ElapsedMilliseconds, response.Usage.InputTokens, response.Usage.OutputTokens);

        return new ExtractionResult(Normalize(invoice), opts.Model, response.Usage.InputTokens,
            response.Usage.OutputTokens, stopwatch.Elapsed);
    }

    private static ContentBlockParam BuildFileBlock(byte[] content, string contentType)
    {
        var base64 = Convert.ToBase64String(content);
        return contentType switch
        {
            "application/pdf" => new DocumentBlockParam { Source = new Base64PdfSource { Data = base64 } },
            "image/jpeg" or "image/jpg" => Image(base64, MediaType.ImageJpeg),
            "image/png" => Image(base64, MediaType.ImagePng),
            "image/webp" => Image(base64, MediaType.ImageWebP),
            _ => throw new ExtractionException($"Unsupported file type '{contentType}'. Use PDF, JPG, PNG or WEBP."),
        };

        static ContentBlockParam Image(string data, MediaType type) =>
            new ImageBlockParam { Source = new Base64ImageSource { Data = data, MediaType = type } };
    }

    private static Effort ParseEffort(string value) => value.ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        _ => Effort.Low,
    };

    private static Dictionary<string, JsonElement> BuildSchema()
    {
        object Nullable(string type) => new { type = new[] { type, "null" } };
        var properties = new Dictionary<string, object>
        {
            ["SupplierName"] = Nullable("string"),
            ["SupplierGstin"] = Nullable("string"),
            ["BuyerGstin"] = Nullable("string"),
            ["InvoiceNumber"] = Nullable("string"),
            ["InvoiceDate"] = Nullable("string"),
            ["TaxableValue"] = Nullable("number"),
            ["Cgst"] = Nullable("number"),
            ["Sgst"] = Nullable("number"),
            ["Igst"] = Nullable("number"),
            ["Total"] = Nullable("number"),
        };
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(properties),
            ["required"] = JsonSerializer.SerializeToElement(properties.Keys),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        };
    }

    private static ExtractedInvoice Normalize(ExtractedInvoice inv) => inv with
    {
        SupplierGstin = inv.SupplierGstin?.Replace(" ", "").ToUpperInvariant(),
        BuyerGstin = inv.BuyerGstin?.Replace(" ", "").ToUpperInvariant(),
        InvoiceNumber = inv.InvoiceNumber?.Trim(),
        SupplierName = inv.SupplierName?.Trim(),
    };
}
