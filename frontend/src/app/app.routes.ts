import { Routes } from '@angular/router';
import { InvoiceList } from './pages/invoice-list/invoice-list';

export const routes: Routes = [
  { path: '', component: InvoiceList, title: 'Invoices' },
  {
    path: 'invoices/:id',
    // Lazy-loaded: the review page is only downloaded when first opened.
    loadComponent: () => import('./pages/invoice-detail/invoice-detail').then(m => m.InvoiceDetailPage),
    title: 'Review invoice',
  },
  { path: '**', redirectTo: '' },
];
