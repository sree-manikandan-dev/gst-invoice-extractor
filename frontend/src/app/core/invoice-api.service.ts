import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { InvoiceDetail, InvoiceFields, InvoiceSummary } from './invoice.models';

/** Thin wrapper over the .NET API. The dev server proxies /api to the backend (see proxy.conf.json). */
@Injectable({ providedIn: 'root' })
export class InvoiceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/invoices';

  list(): Observable<InvoiceSummary[]> {
    return this.http.get<InvoiceSummary[]>(this.base);
  }

  get(id: string): Observable<InvoiceDetail> {
    return this.http.get<InvoiceDetail>(`${this.base}/${id}`);
  }

  fileUrl(id: string): string {
    return `${this.base}/${id}/file`;
  }

  upload(file: File): Observable<InvoiceDetail> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<InvoiceDetail>(this.base, form);
  }

  update(id: string, fields: InvoiceFields): Observable<InvoiceDetail> {
    return this.http.put<InvoiceDetail>(`${this.base}/${id}`, fields);
  }

  approve(id: string): Observable<InvoiceDetail> {
    return this.http.post<InvoiceDetail>(`${this.base}/${id}/approve`, {});
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}

/** Pulls the human-readable message out of an ASP.NET ProblemDetails error. */
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    return err.error?.detail ?? err.error?.title ?? `Request failed (${err.status}).`;
  }
  return 'Something went wrong.';
}
