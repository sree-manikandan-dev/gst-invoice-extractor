namespace GstInvoice.Api.Data;

public enum InvoiceStatus
{
    /// <summary>Extracted, has flags or hasn't been checked by a person yet.</summary>
    NeedsReview,
    Approved,
}

public class Invoice
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public InvoiceStatus Status { get; set; }

    public string? SupplierName { get; set; }
    public string? SupplierGstin { get; set; }
    public string? BuyerGstin { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? TaxableValue { get; set; }
    public decimal? Cgst { get; set; }
    public decimal? Sgst { get; set; }
    public decimal? Igst { get; set; }
    public decimal? Total { get; set; }

    /// <summary>Validation flags, stored as a jsonb column.</summary>
    public List<StoredFlag> Flags { get; set; } = [];

    /// <summary>True once a person has edited any field. Edits are your future training/eval data.</summary>
    public bool WasCorrected { get; set; }

    public string Model { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public int ExtractionMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Kept in its own table so list queries never load file bytes.</summary>
    public InvoiceFile? File { get; set; }
}

public class InvoiceFile
{
    public Guid InvoiceId { get; set; }
    public byte[] Content { get; set; } = [];
}

public class StoredFlag
{
    public string Code { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Field { get; set; }
}
