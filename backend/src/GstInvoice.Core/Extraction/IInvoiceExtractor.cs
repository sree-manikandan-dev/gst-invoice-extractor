using GstInvoice.Core.Models;

namespace GstInvoice.Core.Extraction;

/// <summary>
/// Hides which AI provider reads the invoice. Swap Claude for another model,
/// or for <see cref="FakeInvoiceExtractor"/> in tests, without touching callers.
/// </summary>
public interface IInvoiceExtractor
{
    Task<ExtractionResult> ExtractAsync(byte[] content, string contentType, CancellationToken ct = default);
}

public sealed class ExtractionException(string message, Exception? inner = null) : Exception(message, inner);
