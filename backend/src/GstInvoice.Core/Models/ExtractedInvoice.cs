namespace GstInvoice.Core.Models;

/// <summary>
/// The fields we pull out of one GST invoice. Every field is nullable because
/// a real bill can be blurry, cropped or simply missing a value.
/// </summary>
public sealed record ExtractedInvoice
{
    public string? SupplierName { get; init; }
    public string? SupplierGstin { get; init; }
    public string? BuyerGstin { get; init; }
    public string? InvoiceNumber { get; init; }
    /// <summary>ISO date, yyyy-MM-dd.</summary>
    public string? InvoiceDate { get; init; }
    public decimal? TaxableValue { get; init; }
    public decimal? Cgst { get; init; }
    public decimal? Sgst { get; init; }
    public decimal? Igst { get; init; }
    public decimal? Total { get; init; }
}

/// <summary>What an extractor returns: the fields plus cost/latency data for the README numbers.</summary>
public sealed record ExtractionResult(
    ExtractedInvoice Invoice,
    string Model,
    long InputTokens,
    long OutputTokens,
    TimeSpan Duration);
