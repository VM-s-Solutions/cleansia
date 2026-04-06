import { HelpStep, StatusFlowItem } from '@cleansia/components';
import { EmployeeInvoiceStatus } from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { EmployeeInvoice } from './invoices.facade';

export interface FilterChip {
  key: string;
  label: string;
  value: string;
}

export interface InvoiceStatusOption {
  label: string;
  value: EmployeeInvoiceStatus;
}

// --- Constants ---

export const INVOICES_HELP_STEPS: HelpStep[] = [
  {
    icon: 'pi pi-calendar',
    titleKey: 'help.invoices.step1_title',
    descriptionKey: 'help.invoices.step1_desc',
  },
  {
    icon: 'pi pi-file',
    titleKey: 'help.invoices.step2_title',
    descriptionKey: 'help.invoices.step2_desc',
  },
  {
    icon: 'pi pi-user',
    titleKey: 'help.invoices.step3_title',
    descriptionKey: 'help.invoices.step3_desc',
  },
  {
    icon: 'pi pi-credit-card',
    titleKey: 'help.invoices.step4_title',
    descriptionKey: 'help.invoices.step4_desc',
  },
];

export const INVOICE_STATUS_FLOW: StatusFlowItem[] = [
  {
    statusKey: 'pages.invoices.status_pending',
    descriptionKey: 'help.invoices.status.pending_desc',
    colorClass: 'status-pending',
  },
  {
    statusKey: 'pages.invoices.status_approved',
    descriptionKey: 'help.invoices.status.approved_desc',
    colorClass: 'status-approved',
  },
  {
    statusKey: 'pages.invoices.status_paid',
    descriptionKey: 'help.invoices.status.paid_desc',
    colorClass: 'status-paid',
  },
  {
    statusKey: 'pages.invoices.status_disputed',
    descriptionKey: 'help.invoices.status.disputed_desc',
    colorClass: 'status-disputed',
  },
  {
    statusKey: 'pages.invoices.status_rejected',
    descriptionKey: 'help.invoices.status.rejected_desc',
    colorClass: 'status-rejected',
  },
  {
    statusKey: 'pages.invoices.status_cancelled',
    descriptionKey: 'help.invoices.status.cancelled_desc',
    colorClass: 'status-cancelled',
  },
];

// --- Helper functions ---

export function getInvoiceStatusClass(invoice: EmployeeInvoice): string {
  const statusName = invoice.status.toLowerCase();
  return `status-badge status-${statusName}`;
}

export function buildInvoiceStatusOptions(translate: TranslateService): InvoiceStatusOption[] {
  return [
    { label: translate.instant('pages.invoices.status_pending'), value: EmployeeInvoiceStatus.Pending },
    { label: translate.instant('pages.invoices.status_approved'), value: EmployeeInvoiceStatus.Approved },
    { label: translate.instant('pages.invoices.status_paid'), value: EmployeeInvoiceStatus.Paid },
    { label: translate.instant('pages.invoices.status_disputed'), value: EmployeeInvoiceStatus.Disputed },
    { label: translate.instant('pages.invoices.status_rejected'), value: EmployeeInvoiceStatus.Rejected },
    { label: translate.instant('pages.invoices.status_cancelled'), value: EmployeeInvoiceStatus.Cancelled },
  ];
}

export function buildFilterChips(
  formValue: Record<string, any>,
  statusOptions: InvoiceStatusOption[],
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];

  if (formValue.invoiceNumber) {
    chips.push({
      key: 'invoiceNumber',
      label: translate.instant('pages.invoices.filters.invoice_number'),
      value: formValue.invoiceNumber,
    });
  }

  if (formValue.dateFrom) {
    chips.push({
      key: 'dateFrom',
      label: translate.instant('pages.invoices.filters.date_from'),
      value: new Date(formValue.dateFrom).toLocaleDateString(),
    });
  }

  if (formValue.dateTo) {
    chips.push({
      key: 'dateTo',
      label: translate.instant('pages.invoices.filters.date_to'),
      value: new Date(formValue.dateTo).toLocaleDateString(),
    });
  }

  if (formValue.minAmount != null) {
    chips.push({
      key: 'minAmount',
      label: translate.instant('pages.invoices.filters.min_amount'),
      value: formValue.minAmount.toString(),
    });
  }

  if (formValue.maxAmount != null) {
    chips.push({
      key: 'maxAmount',
      label: translate.instant('pages.invoices.filters.max_amount'),
      value: formValue.maxAmount.toString(),
    });
  }

  if (formValue.statuses && formValue.statuses.length > 0) {
    const statusNames = formValue.statuses
      .map((id: number) => statusOptions.find((o) => o.value === id)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'statuses',
      label: translate.instant('pages.invoices.filters.invoice_status'),
      value: statusNames,
    });
  }

  return chips;
}
