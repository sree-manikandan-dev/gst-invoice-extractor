using GstInvoice.Core.Validation;

namespace GstInvoice.Tests;

public class GstinValidatorTests
{
    [Theory]
    [InlineData("27AAPFU0939F1ZV")] // widely published sample GSTIN
    [InlineData("32AAPFU0939F1Z4")]
    [InlineData("29AABCT1332L1ZA")]
    public void Valid_gstins_pass_every_check(string gstin)
    {
        Assert.True(GstinValidator.HasValidFormat(gstin));
        Assert.True(GstinValidator.HasValidStateCode(gstin));
        Assert.True(GstinValidator.HasValidChecksum(gstin));
    }

    [Theory]
    [InlineData("27AAPFU0939F1ZW")] // last char changed
    [InlineData("27AAPFUO939F1ZV")] // O instead of 0: the classic OCR misread
    public void One_misread_character_fails_the_checksum(string gstin) =>
        Assert.False(GstinValidator.HasValidFormat(gstin) && GstinValidator.HasValidChecksum(gstin));

    [Theory]
    [InlineData("27AAPFU0939F1Z")]   // too short
    [InlineData("27aapfu0939f1zv")]  // lowercase
    [InlineData("AAAPFU0939F1ZVX")]  // no state code
    public void Bad_format_is_rejected(string gstin) => Assert.False(GstinValidator.HasValidFormat(gstin));

    [Fact]
    public void Unknown_state_code_is_rejected() => Assert.False(GstinValidator.HasValidStateCode("99AAPFU0939F1ZV"));
}
