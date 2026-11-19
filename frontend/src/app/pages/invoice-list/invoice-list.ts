import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { InvoiceApiService, errorMessage } from '../../core/invoice-api.service';
import { InvoiceSummary } from '../../core/invoice.models';

@Component({
  selector: 'app-invoice-list',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './invoice-list.html',
  styleUrl: './invoice-list.css',
})
export class InvoiceList implements OnInit {
  private readonly api = inject(InvoiceApiService);
  private readonly router = inject(Router);

  protected readonly invoices = signal<InvoiceSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: rows => {
        this.invoices.set(rows);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.error.set(null);
    this.api.upload(file).subscribe({
      next: invoice => {
        this.uploading.set(false);
        input.value = '';
        // Go straight to review: that's the next thing a user does after uploading.
        this.router.navigate(['/invoices', invoice.id]);
      },
      error: err => {
        this.uploading.set(false);
        input.value = '';
        this.error.set(errorMessage(err));
      },
    });
  }
}
