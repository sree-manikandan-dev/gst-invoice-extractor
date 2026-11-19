using Microsoft.EntityFrameworkCore;

namespace GstInvoice.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceFile> InvoiceFiles => Set<InvoiceFile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Invoice>(e =>
        {
            e.ToTable("invoices");
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.SupplierGstin).HasMaxLength(15);
            e.Property(i => i.BuyerGstin).HasMaxLength(15);
            foreach (var p in new[] { "TaxableValue", "Cgst", "Sgst", "Igst", "Total" })
                e.Property(p).HasPrecision(14, 2);
            e.OwnsMany(i => i.Flags, f => f.ToJson());
            e.HasOne(i => i.File).WithOne().HasForeignKey<InvoiceFile>(f => f.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            // Used by the duplicate check: same supplier + same invoice number.
            e.HasIndex(i => new { i.SupplierGstin, i.InvoiceNumber });
            e.HasIndex(i => i.CreatedAt);
        });
        b.Entity<InvoiceFile>(e =>
        {
            e.ToTable("invoice_files");
            e.HasKey(f => f.InvoiceId);
        });
    }
}
