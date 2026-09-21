import { FilterChip, HelpStep, StatusFlowItem } from '@cleansia/components';
import { EmployeeInvoiceDto, EmployeeInvoiceStatus } from '@cleansia/partner-services';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export interface InvoiceStatusOption {
  label: string;
  value: EmployeeInvoiceStatus;
}

/** The invoices search form's value; every member is optional because a reactive form's `value` omits disabled controls. */
export interface InvoiceFilterFormValue {
  invoiceNumber?: string | null;
  minAmount?: number | null;
  maxAmount?: number | null;
  dateFrom?: Date | null;
  dateTo?: Date | null;
  statuses?: number[] | null;
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

const INVOICE_STATUS_NAMES: Readonly<Record<EmployeeInvoiceStatus, string>> = {
  [EmployeeInvoiceStatus.Pending]: 'pending',
  [EmployeeInvoiceStatus.Approved]: 'approved',
  [EmployeeInvoiceStatus.Paid]: 'paid',
  [EmployeeInvoiceStatus.Disputed]: 'disputed',
  [EmployeeInvoiceStatus.Rejected]: 'rejected',
  [EmployeeInvoiceStatus.Cancelled]: 'cancelled',
};

export function getInvoiceStatusName(status: EmployeeInvoiceStatus): string {
  return INVOICE_STATUS_NAMES[status] ?? INVOICE_STATUS_NAMES[EmployeeInvoiceStatus.Pending];
}

export function getInvoiceStatusLabelKey(invoice: EmployeeInvoiceDto): string {
  return `pages.invoices.status_${getInvoiceStatusName(invoice.status)}`;
}

export function getInvoiceStatusClass(invoice: EmployeeInvoiceDto): string {
  return `status-badge status-${getInvoiceStatusName(invoice.status)}`;
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
  formValue: InvoiceFilterFormValue,
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
      value: formatDate(formValue.dateFrom, translate.currentLang),
    });
  }

  if (formValue.dateTo) {
    chips.push({
      key: 'dateTo',
      label: translate.instant('pages.invoices.filters.date_to'),
      value: formatDate(formValue.dateTo, translate.currentLang),
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
      .map((id) => statusOptions.find((o) => o.value === id)?.label)
      .filter(Boolean)
      .join(', ');
    chips.push({
      key: 'statuses',
      label: translate.instant('pages.invoices.filters.invoice_status'),
      value: statusNames,
      controls: ['statuses', ...statusOptions.map((o) => `status_${o.value}`)],
    });
  }

  return chips;
}
