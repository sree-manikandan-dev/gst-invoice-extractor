using System.Globalization;
using GstInvoice.Api.Data;
using GstInvoice.Core.Extraction;
using GstInvoice.Core.Models;
using GstInvoice.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace GstInvoice.Api.Services;

/// <summary>Upload -> extract -> validate -> save, plus review edits.</summary>
public class InvoiceService(AppDbContext db, IInvoiceExtractor extractor, InvoiceValidator validator, TimeProvider clock)
{
    public static readonly HashSet<string> AllowedTypes = ["application/pdf", "image/jpeg", "image/png", "image/webp"];
    public const long MaxFileBytes = 10 * 1024 * 1024;

    public async Task<InvoiceDetailDto> UploadAsync(string fileName, string contentType, byte[] content, CancellationToken ct)
    {
        var result = await extractor.ExtractAsync(content, contentType, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            Model = result.Model,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            ExtractionMs = (int)result.Duration.TotalMilliseconds,
            CreatedAt = now,
            UpdatedAt = now,
            File = new InvoiceFile { Content = content },
        };
        Apply(invoice, result.Invoice);
        await RevalidateAsync(invoice, result.Invoice, ct);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return InvoiceDetailDto.From(invoice);
    }

    public async Task<IReadOnlyList<InvoiceSummaryDto>> ListAsync(CancellationToken ct)
    {
        // Load flags in memory (they live in a jsonb column) but never the file bytes.
        var rows = await db.Invoices.AsNoTracking().OrderByDescending(i => i.CreatedAt).Take(500).ToListAsync(ct);
        return rows.Select(i => new InvoiceSummaryDto(
            i.Id, i.FileName, i.Status.ToString(), i.SupplierName, i.InvoiceNumber, i.InvoiceDate, i.Total,
            i.Flags.Count(f => f.Severity == nameof(FlagSeverity.Error)),
            i.Flags.Count(f => f.Severity == nameof(FlagSeverity.Warning)),
            i.CreatedAt)).ToList();
    }

    public async Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        return invoice is null ? null : InvoiceDetailDto.From(invoice);
    }

    public async Task<(byte[] Content, string ContentType)?> GetFileAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Invoices.AsNoTracking().Where(i => i.Id == id)
            .Select(i => new { i.ContentType, i.File!.Content }).FirstOrDefaultAsync(ct);
        return row is null ? null : (row.Content, row.ContentType);
    }

    public async Task<InvoiceDetailDto?> UpdateAsync(Guid id, UpdateInvoiceRequest req, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return null;

        var corrected = new ExtractedInvoice
        {
            SupplierName = req.SupplierName?.Trim(),
            SupplierGstin = req.SupplierGstin?.Trim().ToUpperInvariant(),
            BuyerGstin = req.BuyerGstin?.Trim().ToUpperInvariant(),
            InvoiceNumber = req.InvoiceNumber?.Trim(),
            InvoiceDate = req.InvoiceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TaxableValue = req.TaxableValue, Cgst = req.Cgst, Sgst = req.Sgst, Igst = req.Igst, Total = req.Total,
        };
        Apply(invoice, corrected);
        invoice.WasCorrected = true;
        invoice.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await RevalidateAsync(invoice, corrected, ct);

        await db.SaveChangesAsync(ct);
        return InvoiceDetailDto.From(invoice);
    }

    /// <summary>Approve only when no error flags remain; warnings are allowed.</summary>
    public async Task<(InvoiceDetailDto? Invoice, string? Error)> ApproveAsync(Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return (null, null);
        if (invoice.Flags.Any(f => f.Severity == nameof(FlagSeverity.Error)))
            return (null, "Fix all error flags before approving.");

        invoice.Status = InvoiceStatus.Approved;
        invoice.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return (InvoiceDetailDto.From(invoice), null);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return false;
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task RevalidateAsync(Invoice invoice, ExtractedInvoice fields, CancellationToken ct)
    {
        var flags = validator.Validate(fields, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)).ToList();

        // Duplicate check needs the database, so it lives here rather than in the pure validator.
        if (!string.IsNullOrEmpty(fields.SupplierGstin) && !string.IsNullOrEmpty(fields.InvoiceNumber))
        {
            var duplicate = await db.Invoices.AnyAsync(i => i.Id != invoice.Id
                && i.SupplierGstin == fields.SupplierGstin && i.InvoiceNumber == fields.InvoiceNumber, ct);
            if (duplicate)
                flags.Add(new("DUPLICATE", FlagSeverity.Error,
                    "Another invoice from this supplier has the same invoice number.", "InvoiceNumber"));
        }

        invoice.Flags = flags.Select(f => new StoredFlag
        {
            Code = f.Code, Severity = f.Severity.ToString(), Message = f.Message, Field = f.Field,
        }).ToList();
        invoice.Status = InvoiceStatus.NeedsReview;
    }

    private static void Apply(Invoice target, ExtractedInvoice source)
    {
        target.SupplierName = source.SupplierName;
        target.SupplierGstin = source.SupplierGstin;
        target.BuyerGstin = source.BuyerGstin;
        target.InvoiceNumber = source.InvoiceNumber;
        target.InvoiceDate = DateOnly.TryParseExact(source.InvoiceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;
        target.TaxableValue = source.TaxableValue;
        target.Cgst = source.Cgst;
        target.Sgst = source.Sgst;
        target.Igst = source.Igst;
        target.Total = source.Total;
    }
}
