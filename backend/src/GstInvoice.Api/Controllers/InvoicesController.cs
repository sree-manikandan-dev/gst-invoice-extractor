using GstInvoice.Api.Services;
using GstInvoice.Core.Extraction;
using Microsoft.AspNetCore.Mvc;

namespace GstInvoice.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(InvoiceService service, ILogger<InvoicesController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(InvoiceService.MaxFileBytes + 1024 * 1024)]
    public async Task<ActionResult<InvoiceDetailDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return Problem("The file is empty.", statusCode: 400);
        if (file.Length > InvoiceService.MaxFileBytes) return Problem("Files must be 10 MB or smaller.", statusCode: 400);
        if (!InvoiceService.AllowedTypes.Contains(file.ContentType))
            return Problem("Upload a PDF, JPG, PNG or WEBP file.", statusCode: 415);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        try
        {
            var invoice = await service.UploadAsync(Path.GetFileName(file.FileName), file.ContentType, ms.ToArray(), ct);
            return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
        }
        catch (ExtractionException ex)
        {
            logger.LogWarning(ex, "Extraction failed for {File}", file.FileName);
            return Problem(ex.Message, statusCode: 502, title: "Extraction failed");
        }
    }

    [HttpGet]
    public Task<IReadOnlyList<InvoiceSummaryDto>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDetailDto>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct) is { } invoice ? invoice : NotFound();

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken ct) =>
        await service.GetFileAsync(id, ct) is { } file ? File(file.Content, file.ContentType) : NotFound();

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceDetailDto>> Update(Guid id, UpdateInvoiceRequest request, CancellationToken ct) =>
        await service.UpdateAsync(id, request, ct) is { } invoice ? invoice : NotFound();

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<InvoiceDetailDto>> Approve(Guid id, CancellationToken ct)
    {
        var (invoice, error) = await service.ApproveAsync(id, ct);
        if (error is not null) return Problem(error, statusCode: 409);
        return invoice is null ? NotFound() : invoice;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
