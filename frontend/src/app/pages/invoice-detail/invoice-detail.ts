import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { DomSanitizer } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { InvoiceApiService, errorMessage } from '../../core/invoice-api.service';
import { InvoiceDetail, InvoiceFields } from '../../core/invoice.models';

type FieldKey = keyof InvoiceFields;

@Component({
  selector: 'app-invoice-detail',
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe],
  templateUrl: './invoice-detail.html',
  styleUrl: './invoice-detail.css',
})
export class InvoiceDetailPage implements OnInit {
  /** Bound from the route parameter :id (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(InvoiceApiService);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly fb = inject(FormBuilder);

  protected readonly invoice = signal<InvoiceDetail | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);

  protected readonly form = this.fb.group({
    supplierName: this.fb.control<string | null>(null),
    supplierGstin: this.fb.control<string | null>(null),
    buyerGstin: this.fb.control<string | null>(null),
    invoiceNumber: this.fb.control<string | null>(null),
    invoiceDate: this.fb.control<string | null>(null),
    taxableValue: this.fb.control<number | null>(null),
    cgst: this.fb.control<number | null>(null),
    sgst: this.fb.control<number | null>(null),
    igst: this.fb.control<number | null>(null),
    total: this.fb.control<number | null>(null),
  });

  protected readonly fields: { key: FieldKey; label: string; type: 'text' | 'date' | 'number' }[] = [
    { key: 'supplierName', label: 'Supplier name', type: 'text' },
    { key: 'supplierGstin', label: 'Supplier GSTIN', type: 'text' },
    { key: 'buyerGstin', label: 'Buyer GSTIN', type: 'text' },
    { key: 'invoiceNumber', label: 'Invoice number', type: 'text' },
    { key: 'invoiceDate', label: 'Invoice date', type: 'date' },
    { key: 'taxableValue', label: 'Taxable value (₹)', type: 'number' },
    { key: 'cgst', label: 'CGST (₹)', type: 'number' },
    { key: 'sgst', label: 'SGST (₹)', type: 'number' },
    { key: 'igst', label: 'IGST (₹)', type: 'number' },
    { key: 'total', label: 'Total (₹)', type: 'number' },
  ];

  protected readonly fileUrl = computed(() => this.api.fileUrl(this.id()));
  protected readonly pdfUrl = computed(() => this.sanitizer.bypassSecurityTrustResourceUrl(this.fileUrl()));
  protected readonly isPdf = computed(() => this.invoice()?.contentType === 'application/pdf');
  protected readonly hasErrors = computed(() => this.invoice()?.flags.some(f => f.severity === 'Error') ?? false);

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: inv => this.show(inv),
      error: err => this.error.set(errorMessage(err)),
    });
  }

  /** Field names come back PascalCase from the validator (e.g. "SupplierGstin"). */
  protected flagsFor(key: FieldKey) {
    return this.invoice()?.flags.filter(f => f.field?.toLowerCase() === key.toLowerCase()) ?? [];
  }

  save(): void {
    this.run(this.api.update(this.id(), this.form.getRawValue() as InvoiceFields), 'Saved and re-checked.');
  }

  approve(): void {
    this.run(this.api.approve(this.id()), 'Approved.');
  }

  remove(): void {
    this.api.delete(this.id()).subscribe({
      next: () => this.router.navigate(['/']),
      error: err => this.error.set(errorMessage(err)),
    });
  }

  private run(request: ReturnType<InvoiceApiService['get']>, done: string): void {
    this.saving.set(true);
    this.error.set(null);
    this.message.set(null);
    request.subscribe({
      next: inv => {
        this.show(inv);
        this.saving.set(false);
        this.message.set(done);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }

  private show(inv: InvoiceDetail): void {
    this.invoice.set(inv);
    this.form.reset({
      supplierName: inv.supplierName,
      supplierGstin: inv.supplierGstin,
      buyerGstin: inv.buyerGstin,
      invoiceNumber: inv.invoiceNumber,
      invoiceDate: inv.invoiceDate,
      taxableValue: inv.taxableValue,
      cgst: inv.cgst,
      sgst: inv.sgst,
      igst: inv.igst,
      total: inv.total,
    });
  }
}
