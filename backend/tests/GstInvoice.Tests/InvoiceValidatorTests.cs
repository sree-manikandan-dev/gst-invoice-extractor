using GstInvoice.Core.Extraction;
using GstInvoice.Core.Models;
using GstInvoice.Core.Validation;

namespace GstInvoice.Tests;

public class InvoiceValidatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);
    private readonly InvoiceValidator _validator = new();

    private static ExtractedInvoice Valid => FakeInvoiceExtractor.Sample;

    private IEnumerable<string> Codes(ExtractedInvoice inv) => _validator.Validate(inv, Today).Select(f => f.Code);

    [Fact]
    public void Clean_invoice_has_no_flags() => Assert.Empty(_validator.Validate(Valid, Today));

    [Fact]
    public void Missing_required_fields_are_flagged()
    {
        var flags = _validator.Validate(new ExtractedInvoice(), Today);
        Assert.Equal(4, flags.Count(f => f.Code == "MISSING_FIELD"));
    }

    [Fact]
    public void Total_that_does_not_add_up_is_flagged() =>
        Assert.Contains("TOTAL_MISMATCH", Codes(Valid with { Total = 1280m }));

    [Fact]
    public void Rounding_within_one_rupee_is_allowed() =>
        Assert.DoesNotContain("TOTAL_MISMATCH", Codes(Valid with { Total = 1180.60m }));

    [Fact]
    public void Igst_and_cgst_together_is_flagged() =>
        Assert.Contains("TAX_BOTH_TYPES", Codes(Valid with { Igst = 180m }));

    [Fact]
    public void Unequal_cgst_and_sgst_is_flagged() =>
        Assert.Contains("CGST_SGST_MISMATCH", Codes(Valid with { Sgst = 80m, Total = 1170m }));

    [Fact]
    public void Same_state_with_igst_is_flagged()
    {
        var inv = Valid with { Cgst = 0, Sgst = 0, Igst = 180m };
        Assert.Contains("WRONG_TAX_TYPE", Codes(inv));
    }

    [Fact]
    public void Different_state_with_cgst_is_flagged() =>
        Assert.Contains("WRONG_TAX_TYPE", Codes(Valid with { BuyerGstin = "29AABCT1332L1ZA" }));

    [Fact]
    public void Future_date_is_flagged() =>
        Assert.Contains("DATE_FUTURE", Codes(Valid with { InvoiceDate = "2027-01-01" }));

    [Fact]
    public void Non_iso_date_is_flagged() =>
        Assert.Contains("DATE_FORMAT", Codes(Valid with { InvoiceDate = "15/10/2026" }));

    [Fact]
    public void Misread_gstin_is_flagged() =>
        Assert.Contains("GSTIN_CHECKSUM", Codes(Valid with { SupplierGstin = "32AAPFU0939F1Z5" }));

    [Fact]
    public void Unusual_rate_is_only_a_warning()
    {
        var flags = _validator.Validate(Valid with { Cgst = 70m, Sgst = 70m, Total = 1140m }, Today);
        var flag = Assert.Single(flags);
        Assert.Equal("UNUSUAL_RATE", flag.Code);
        Assert.Equal(FlagSeverity.Warning, flag.Severity);
    }
}
