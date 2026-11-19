using System.Globalization;
using GstInvoice.Core.Models;

namespace GstInvoice.Core.Validation;

/// <summary>
/// Deterministic checks that run AFTER the LLM. The model reads the bill; this code
/// decides whether the numbers make sense. Nothing here throws: every problem becomes
/// a flag that the review screen shows to a human.
/// </summary>
public sealed class InvoiceValidator
{
    /// <summary>Rupee tolerance for rounding on printed bills.</summary>
    public const decimal Tolerance = 1.00m;

    // Common GST slab rates in percent (combined CGST+SGST or IGST), incl. the 40% slab from Sept 2025.
    private static readonly decimal[] KnownRates = [0m, 0.25m, 3m, 5m, 12m, 18m, 28m, 40m];

    public IReadOnlyList<ValidationFlag> Validate(ExtractedInvoice inv, DateOnly? today = null)
    {
        var flags = new List<ValidationFlag>();
        var now = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

        RequireField(flags, inv.SupplierGstin, nameof(inv.SupplierGstin));
        RequireField(flags, inv.InvoiceNumber, nameof(inv.InvoiceNumber));
        RequireField(flags, inv.InvoiceDate, nameof(inv.InvoiceDate));
        if (inv.Total is null)
            flags.Add(new("MISSING_FIELD", FlagSeverity.Error, "Total is missing.", nameof(inv.Total)));

        CheckGstin(flags, inv.SupplierGstin, nameof(inv.SupplierGstin));
        CheckGstin(flags, inv.BuyerGstin, nameof(inv.BuyerGstin));
        CheckDate(flags, inv.InvoiceDate, now);
        CheckTaxSplit(flags, inv);
        CheckTotals(flags, inv);
        CheckRate(flags, inv);

        return flags;
    }

    private static void RequireField(List<ValidationFlag> flags, string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            flags.Add(new("MISSING_FIELD", FlagSeverity.Error, $"{field} is missing.", field));
    }

    private static void CheckGstin(List<ValidationFlag> flags, string? gstin, string field)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return;

        if (!GstinValidator.HasValidFormat(gstin))
        {
            flags.Add(new("GSTIN_FORMAT", FlagSeverity.Error, $"{gstin} is not a valid GSTIN format.", field));
            return;
        }
        if (!GstinValidator.HasValidStateCode(gstin))
            flags.Add(new("GSTIN_STATE", FlagSeverity.Error, $"{gstin} has an unknown state code.", field));
        if (!GstinValidator.HasValidChecksum(gstin))
            flags.Add(new("GSTIN_CHECKSUM", FlagSeverity.Error,
                $"{gstin} fails the checksum; a character was probably misread.", field));
    }

    private static void CheckDate(List<ValidationFlag> flags, string? date, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(date)) return;

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            flags.Add(new("DATE_FORMAT", FlagSeverity.Error, $"'{date}' is not a valid date.", "InvoiceDate"));
            return;
        }
        if (parsed > today)
            flags.Add(new("DATE_FUTURE", FlagSeverity.Error, "Invoice date is in the future.", "InvoiceDate"));
    }

    private static void CheckTaxSplit(List<ValidationFlag> flags, ExtractedInvoice inv)
    {
        var cgst = inv.Cgst ?? 0;
        var sgst = inv.Sgst ?? 0;
        var igst = inv.Igst ?? 0;

        if (igst > 0 && (cgst > 0 || sgst > 0))
            flags.Add(new("TAX_BOTH_TYPES", FlagSeverity.Error,
                "Invoice has both IGST and CGST/SGST; it should be one or the other."));

        if (Math.Abs(cgst - sgst) > Tolerance)
            flags.Add(new("CGST_SGST_MISMATCH", FlagSeverity.Error, "CGST and SGST should be equal.", "Sgst"));

        // Same state => intra-state supply => CGST+SGST. Different state => IGST.
        var supplierState = GstinValidator.StateCode(inv.SupplierGstin);
        var buyerState = GstinValidator.StateCode(inv.BuyerGstin);
        if (supplierState is null || buyerState is null) return;

        if (supplierState == buyerState && igst > 0)
            flags.Add(new("WRONG_TAX_TYPE", FlagSeverity.Error,
                "Supplier and buyer are in the same state, so tax should be CGST+SGST, not IGST.", "Igst"));
        if (supplierState != buyerState && (cgst > 0 || sgst > 0))
            flags.Add(new("WRONG_TAX_TYPE", FlagSeverity.Error,
                "Supplier and buyer are in different states, so tax should be IGST.", "Cgst"));
    }

    private static void CheckTotals(List<ValidationFlag> flags, ExtractedInvoice inv)
    {
        if (inv.TaxableValue is null || inv.Total is null) return;

        var expected = inv.TaxableValue.Value + (inv.Cgst ?? 0) + (inv.Sgst ?? 0) + (inv.Igst ?? 0);
        if (Math.Abs(expected - inv.Total.Value) > Tolerance)
            flags.Add(new("TOTAL_MISMATCH", FlagSeverity.Error,
                $"Taxable value + taxes = {expected:0.00}, but total is {inv.Total:0.00}.", "Total"));
    }

    private static void CheckRate(List<ValidationFlag> flags, ExtractedInvoice inv)
    {
        if (inv.TaxableValue is not > 0) return;

        var tax = (inv.Cgst ?? 0) + (inv.Sgst ?? 0) + (inv.Igst ?? 0);
        var rate = tax / inv.TaxableValue.Value * 100;
        if (!KnownRates.Any(r => Math.Abs(r - rate) <= 0.5m))
            flags.Add(new("UNUSUAL_RATE", FlagSeverity.Warning,
                $"Effective tax rate is {rate:0.##}%. Fine for mixed-rate bills, otherwise check the amounts."));
    }
}
