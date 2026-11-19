using GstInvoice.Core.Models;

namespace GstInvoice.Core.Evaluation;

public sealed record FieldResult(string Field, string? Expected, string? Actual, bool Correct);

/// <summary>
/// Compares an extracted invoice with a hand-labelled one, field by field.
/// Strings: case- and space-insensitive exact match. Amounts: within 50 paise.
/// A field that is null in both counts as correct (the model rightly said "not present").
/// </summary>
public static class FieldComparer
{
    public const decimal AmountTolerance = 0.50m;

    public static IReadOnlyList<FieldResult> Compare(ExtractedInvoice expected, ExtractedInvoice actual) =>
    [
        Text(nameof(expected.SupplierName), expected.SupplierName, actual.SupplierName),
        Text(nameof(expected.SupplierGstin), expected.SupplierGstin, actual.SupplierGstin),
        Text(nameof(expected.BuyerGstin), expected.BuyerGstin, actual.BuyerGstin),
        Text(nameof(expected.InvoiceNumber), expected.InvoiceNumber, actual.InvoiceNumber),
        Text(nameof(expected.InvoiceDate), expected.InvoiceDate, actual.InvoiceDate),
        Amount(nameof(expected.TaxableValue), expected.TaxableValue, actual.TaxableValue),
        Amount(nameof(expected.Cgst), expected.Cgst, actual.Cgst),
        Amount(nameof(expected.Sgst), expected.Sgst, actual.Sgst),
        Amount(nameof(expected.Igst), expected.Igst, actual.Igst),
        Amount(nameof(expected.Total), expected.Total, actual.Total),
    ];

    private static string? Norm(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : string.Concat(s.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static FieldResult Text(string field, string? expected, string? actual) =>
        new(field, expected, actual, Norm(expected) == Norm(actual));

    private static FieldResult Amount(string field, decimal? expected, decimal? actual)
    {
        // Treat a missing amount and 0 as the same thing (e.g. IGST on an intra-state bill).
        var e = expected ?? 0;
        var a = actual ?? 0;
        return new(field, expected?.ToString("0.00"), actual?.ToString("0.00"), Math.Abs(e - a) <= AmountTolerance);
    }
}
