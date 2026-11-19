using GstInvoice.Core.Models;

namespace GstInvoice.Core.Extraction;

/// <summary>Returns a fixed sample invoice. Lets the app run and be demoed without an API key.</summary>
public sealed class FakeInvoiceExtractor : IInvoiceExtractor
{
    public static readonly ExtractedInvoice Sample = new()
    {
        SupplierName = "Sample Traders",
        SupplierGstin = "32AAPFU0939F1Z4",
        BuyerGstin = "32ABCDE1234F1Z9",
        InvoiceNumber = "ST/2026/0042",
        InvoiceDate = "2026-10-15",
        TaxableValue = 1000.00m,
        Cgst = 90.00m,
        Sgst = 90.00m,
        Igst = 0m,
        Total = 1180.00m,
    };

    public Task<ExtractionResult> ExtractAsync(byte[] content, string contentType, CancellationToken ct = default) =>
        Task.FromResult(new ExtractionResult(Sample, "fake", 0, 0, TimeSpan.Zero));
}
