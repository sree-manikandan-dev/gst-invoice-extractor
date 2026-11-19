using GstInvoice.Core.Evaluation;
using GstInvoice.Core.Extraction;

namespace GstInvoice.Tests;

public class FieldComparerTests
{
    [Fact]
    public void Identical_invoices_score_every_field_correct() =>
        Assert.All(FieldComparer.Compare(FakeInvoiceExtractor.Sample, FakeInvoiceExtractor.Sample), r => Assert.True(r.Correct));

    [Fact]
    public void Case_and_spaces_are_ignored_for_text()
    {
        var actual = FakeInvoiceExtractor.Sample with { SupplierName = "  sample  traders " };
        var result = FieldComparer.Compare(FakeInvoiceExtractor.Sample, actual).Single(r => r.Field == "SupplierName");
        Assert.True(result.Correct);
    }

    [Fact]
    public void Amount_off_by_more_than_50_paise_is_wrong()
    {
        var actual = FakeInvoiceExtractor.Sample with { Total = 1181m };
        Assert.False(FieldComparer.Compare(FakeInvoiceExtractor.Sample, actual).Single(r => r.Field == "Total").Correct);
    }

    [Fact]
    public void Null_and_zero_amount_are_treated_the_same()
    {
        var actual = FakeInvoiceExtractor.Sample with { Igst = null };
        Assert.True(FieldComparer.Compare(FakeInvoiceExtractor.Sample, actual).Single(r => r.Field == "Igst").Correct);
    }
}
