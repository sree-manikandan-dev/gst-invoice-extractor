using System.ComponentModel.DataAnnotations;
using GstInvoice.Api.Data;

namespace GstInvoice.Api.Services;

public record FlagDto(string Code, string Severity, string Message, string? Field);

public record InvoiceSummaryDto(
    Guid Id, string FileName, string Status, string? SupplierName, string? InvoiceNumber,
    DateOnly? InvoiceDate, decimal? Total, int ErrorCount, int WarningCount, DateTime CreatedAt);

public record InvoiceDetailDto(
    Guid Id, string FileName, string ContentType, string Status,
    string? SupplierName, string? SupplierGstin, string? BuyerGstin, string? InvoiceNumber, DateOnly? InvoiceDate,
    decimal? TaxableValue, decimal? Cgst, decimal? Sgst, decimal? Igst, decimal? Total,
    IReadOnlyList<FlagDto> Flags, bool WasCorrected, string Model, long InputTokens, long OutputTokens,
    int ExtractionMs, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static InvoiceDetailDto From(Invoice i) => new(
        i.Id, i.FileName, i.ContentType, i.Status.ToString(), i.SupplierName, i.SupplierGstin, i.BuyerGstin,
        i.InvoiceNumber, i.InvoiceDate, i.TaxableValue, i.Cgst, i.Sgst, i.Igst, i.Total,
        i.Flags.Select(f => new FlagDto(f.Code, f.Severity, f.Message, f.Field)).ToList(),
        i.WasCorrected, i.Model, i.InputTokens, i.OutputTokens, i.ExtractionMs, i.CreatedAt, i.UpdatedAt);
}

/// <summary>Fields a reviewer can correct. Re-validated on save.</summary>
public record UpdateInvoiceRequest(
    [property: MaxLength(200)] string? SupplierName,
    [property: MaxLength(15)] string? SupplierGstin,
    [property: MaxLength(15)] string? BuyerGstin,
    [property: MaxLength(50)] string? InvoiceNumber,
    DateOnly? InvoiceDate,
    decimal? TaxableValue, decimal? Cgst, decimal? Sgst, decimal? Igst, decimal? Total);
