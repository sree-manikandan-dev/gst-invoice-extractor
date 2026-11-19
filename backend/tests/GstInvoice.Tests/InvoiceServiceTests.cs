using GstInvoice.Api.Data;
using GstInvoice.Api.Services;
using GstInvoice.Core.Extraction;
using GstInvoice.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace GstInvoice.Tests;

public class InvoiceServiceTests
{
    private static InvoiceService CreateService(out AppDbContext db)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        db = new AppDbContext(options);
        return new InvoiceService(db, new FakeInvoiceExtractor(), new InvoiceValidator(), TimeProvider.System);
    }

    [Fact]
    public async Task Upload_saves_extracted_fields_and_needs_review()
    {
        var service = CreateService(out _);
        var dto = await service.UploadAsync("bill.png", "image/png", [1, 2, 3], CancellationToken.None);

        Assert.Equal("NeedsReview", dto.Status);
        Assert.Equal("32AAPFU0939F1Z4", dto.SupplierGstin);
        Assert.Empty(dto.Flags);
    }

    [Fact]
    public async Task Second_upload_of_same_invoice_is_flagged_as_duplicate()
    {
        var service = CreateService(out _);
        await service.UploadAsync("a.png", "image/png", [1], CancellationToken.None);
        var second = await service.UploadAsync("b.png", "image/png", [1], CancellationToken.None);

        Assert.Contains(second.Flags, f => f.Code == "DUPLICATE");
    }

    [Fact]
    public async Task Correction_revalidates_and_marks_invoice_corrected()
    {
        var service = CreateService(out _);
        var dto = await service.UploadAsync("a.png", "image/png", [1], CancellationToken.None);

        var updated = await service.UpdateAsync(dto.Id, new UpdateInvoiceRequest(
            dto.SupplierName, dto.SupplierGstin, dto.BuyerGstin, dto.InvoiceNumber, dto.InvoiceDate,
            dto.TaxableValue, dto.Cgst, dto.Sgst, dto.Igst, Total: 9999m), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated.WasCorrected);
        Assert.Contains(updated.Flags, f => f.Code == "TOTAL_MISMATCH");
    }

    [Fact]
    public async Task Cannot_approve_while_error_flags_remain()
    {
        var service = CreateService(out _);
        await service.UploadAsync("a.png", "image/png", [1], CancellationToken.None);
        var duplicate = await service.UploadAsync("b.png", "image/png", [1], CancellationToken.None);

        var (_, error) = await service.ApproveAsync(duplicate.Id, CancellationToken.None);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Clean_invoice_can_be_approved()
    {
        var service = CreateService(out _);
        var dto = await service.UploadAsync("a.png", "image/png", [1], CancellationToken.None);

        var (approved, error) = await service.ApproveAsync(dto.Id, CancellationToken.None);
        Assert.Null(error);
        Assert.Equal("Approved", approved!.Status);
    }
}
