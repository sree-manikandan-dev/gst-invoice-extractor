using GstInvoice.Core.Models;

namespace GstInvoice.Core.Extraction;

/// <summary>Prompt and post-processing shared by every AI provider, so results are comparable.</summary>
public static class ExtractionPrompt
{
    public const string Instructions = """
        You are reading one Indian GST purchase invoice. Extract the fields into the JSON schema.
        Rules:
        - Copy GSTINs exactly as printed (15 characters, uppercase). Do not guess characters you cannot read.
        - InvoiceDate must be yyyy-MM-dd. Indian bills usually print dd/mm/yyyy.
        - Amounts are plain numbers in rupees, no commas or currency symbols.
        - TaxableValue is the total before tax. Total is the final invoice amount.
        - If a field is not on the invoice or is unreadable, use null. Never invent values.
        - BuyerGstin is the recipient's GSTIN ("Bill to"), if printed.
        """;

    public static ExtractedInvoice Normalize(ExtractedInvoice inv) => inv with
    {
        SupplierGstin = inv.SupplierGstin?.Replace(" ", "").ToUpperInvariant(),
        BuyerGstin = inv.BuyerGstin?.Replace(" ", "").ToUpperInvariant(),
        InvoiceNumber = inv.InvoiceNumber?.Trim(),
        SupplierName = inv.SupplierName?.Trim(),
    };
}
