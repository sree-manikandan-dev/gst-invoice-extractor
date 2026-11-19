export interface Flag {
  code: string;
  severity: 'Error' | 'Warning';
  message: string;
  field: string | null;
}

export interface InvoiceSummary {
  id: string;
  fileName: string;
  status: 'NeedsReview' | 'Approved';
  supplierName: string | null;
  invoiceNumber: string | null;
  invoiceDate: string | null;
  total: number | null;
  errorCount: number;
  warningCount: number;
  createdAt: string;
}

/** The fields a reviewer can edit. */
export interface InvoiceFields {
  supplierName: string | null;
  supplierGstin: string | null;
  buyerGstin: string | null;
  invoiceNumber: string | null;
  invoiceDate: string | null;
  taxableValue: number | null;
  cgst: number | null;
  sgst: number | null;
  igst: number | null;
  total: number | null;
}

export interface InvoiceDetail extends InvoiceFields {
  id: string;
  fileName: string;
  contentType: string;
  status: 'NeedsReview' | 'Approved';
  flags: Flag[];
  wasCorrected: boolean;
  model: string;
  inputTokens: number;
  outputTokens: number;
  extractionMs: number;
  createdAt: string;
  updatedAt: string;
}
