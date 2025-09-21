import { Injectable, inject, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';

export interface Invoice {
  id: number;
  number: string;
  date: Date;
  amount: number;
  vat: number;
  total: number;
  dueDate: Date;
  status: 'Pending' | 'Paid' | 'Overdue';
}

@Injectable()
export class InvoicesFacade {
  private readonly messageService = inject(MessageService);

  // Signals for reactive data
  pendingInvoices = signal<Invoice[]>([
    // Sample data; replace with API fetch
    { id: 1, number: 'INV-2025-001', date: new Date('2025-09-01'), amount: 15000, vat: 3150, total: 18150, dueDate: new Date('2025-09-30'), status: 'Pending' },
  ]);
  allInvoices = signal<Invoice[]>([
    // Sample data; replace with API fetch
    { id: 1, number: 'INV-2025-001', date: new Date('2025-09-01'), amount: 15000, vat: 3150, total: 18150, dueDate: new Date('2025-09-30'), status: 'Pending' },
    { id: 2, number: 'INV-2025-002', date: new Date('2025-08-01'), amount: 12000, vat: 2520, total: 14520, dueDate: new Date('2025-08-30'), status: 'Paid' },
    { id: 3, number: 'INV-2025-003', date: new Date('2025-07-01'), amount: 18000, vat: 3780, total: 21780, dueDate: new Date('2025-07-30'), status: 'Overdue' },
  ]);
  showDetailsDialog = signal(false);
  selectedInvoice = signal<Invoice | null>(null);
  isGenerating = signal(false);

  generateFromTimeLogs(): void {
    this.isGenerating.set(true);
    // Simulate generating invoice from recent time logs
    setTimeout(() => {
      const newInvoice: Invoice = {
        id: Date.now(),
        number: `INV-2025-${(this.pendingInvoices().length + 1).toString().padStart(3, '0')}`,
        date: new Date(),
        amount: 20000, // Mock from logs; integrate with OrdersFacade
        vat: 4200, // 21% CZ VAT
        total: 24200,
        dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000), // 30 days
        status: 'Pending'
      };
      this.pendingInvoices.update(prev => [...prev, newInvoice]);
      this.allInvoices.update(prev => [...prev, newInvoice]);
      this.messageService.add({
        severity: 'success',
        summary: 'Generated',
        detail: 'Invoice generated from time logs.'
      });
      this.isGenerating.set(false);
    }, 2000);
  }

  previewInvoice(): void {
    // Simulate PDF preview (e.g., open modal or window)
    this.messageService.add({
      severity: 'info',
      summary: 'Preview',
      detail: 'Invoice preview opened.'
    });
    // Integrate with jsPDF or similar for actual preview
  }

  public downloadInvoice(invoice: Invoice): void {
    // Simulate download
    const link = document.createElement('a');
    link.href = `/api/invoices/${invoice.id}/pdf`; // Mock URL
    link.download = `${invoice.number}.pdf`;
    link.click();
    this.messageService.add({
      severity: 'success',
      summary: 'Downloaded',
      detail: 'Invoice downloaded successfully.'
    });
  }

  public viewInvoiceDetails(invoice: Invoice): void {
    this.selectedInvoice.set(invoice);
    this.showDetailsDialog.set(true);
  }

  resetDetailsDialog(): void {
    this.selectedInvoice.set(null);
  }
}