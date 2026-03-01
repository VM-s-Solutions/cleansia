import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaHelpCardComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  HelpStep,
  StatusFlowItem,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { EmployeeInvoiceStatus, SortDefinition, SortDirection } from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { EmployeeInvoice, InvoicesFacade } from './invoices.facade';
import { getInvoicesTableDefinition } from './invoices.models';

interface FilterChip {
  key: string;
  label: string;
  value: string;
}

@Component({
  selector: 'cleansia-partner-invoices',
  standalone: true,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaSectionComponent,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaTextInputComponent,
    CleansiaCalendarComponent,
    CleansiaHelpCardComponent,
  ],
  templateUrl: './invoices.component.html',
  providers: [InvoicesFacade],
})
export class InvoicesComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  protected readonly facade = inject(InvoicesFacade);
  private readonly translate = inject(TranslateService);
  private readonly destroy$ = new Subject<void>();

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');
  invoicesHelpCard = viewChild<CleansiaHelpCardComponent>('invoicesHelpCard');

  invoicesColumns!: TableColumn<EmployeeInvoice>[];
  invoicesActions!: TableAction<EmployeeInvoice>[];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;

  // Filter drawer state
  isFilterDrawerOpen = signal(false);

  // Help card dismissal state
  private helpDismissedVersion = signal(0);
  isInvoicesHelpDismissed = computed(() => {
    this.helpDismissedVersion(); // Track for reactivity
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-invoices-help-dismissed');
  });

  // Search form
  searchForm = this.fb.group({
    // Search fields
    invoiceNumber: [''],
    minAmount: [null as number | null],
    maxAmount: [null as number | null],
    dateFrom: [null as Date | null],
    dateTo: [null as Date | null],
    // Invoice Status checkboxes
    status_1: [false], // Pending
    status_2: [false], // Approved
    status_3: [false], // Paid
    status_4: [false], // Disputed
    status_5: [false], // Rejected
    status_6: [false], // Cancelled
    // Internal status array
    statuses: [[] as number[]],
  });

  // Status options for checkboxes
  invoiceStatusOptions = [
    { label: this.translate.instant('pages.invoices.status_pending'), value: EmployeeInvoiceStatus.Pending },
    { label: this.translate.instant('pages.invoices.status_approved'), value: EmployeeInvoiceStatus.Approved },
    { label: this.translate.instant('pages.invoices.status_paid'), value: EmployeeInvoiceStatus.Paid },
    { label: this.translate.instant('pages.invoices.status_disputed'), value: EmployeeInvoiceStatus.Disputed },
    { label: this.translate.instant('pages.invoices.status_rejected'), value: EmployeeInvoiceStatus.Rejected },
    { label: this.translate.instant('pages.invoices.status_cancelled'), value: EmployeeInvoiceStatus.Cancelled },
  ];

  // Help card steps for invoices workflow
  invoicesHelpSteps: HelpStep[] = [
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

  // Invoice status flow explanations
  invoiceStatusFlow: StatusFlowItem[] = [
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

  // Filter reactivity - increment this to trigger computed updates
  private filterFormVersion = signal(0);

  // Active filter chips - depend on filterFormVersion for reactivity
  activeFilterChips = computed(() => {
    this.filterFormVersion(); // Track this signal for reactivity
    return this.getActiveFilterChips();
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    // Update filter version on every form change for reactive filter chips
    this.searchForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    // Setup automatic filtering with debounce
    this.searchForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Rebuild tables and filters when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      });
  }

  private rebuildTableDefinitions(): void {
    const def = getInvoicesTableDefinition(
      {
        onDownload: this.downloadInvoice.bind(this),
      },
      this.statusTemplate()
    );
    this.invoicesColumns = def.columns;
    this.invoicesActions = def.actions;
  }

  private rebuildFilterOptions(): void {
    this.invoiceStatusOptions = [
      { label: this.translate.instant('pages.invoices.status_pending'), value: EmployeeInvoiceStatus.Pending },
      { label: this.translate.instant('pages.invoices.status_approved'), value: EmployeeInvoiceStatus.Approved },
      { label: this.translate.instant('pages.invoices.status_paid'), value: EmployeeInvoiceStatus.Paid },
      { label: this.translate.instant('pages.invoices.status_disputed'), value: EmployeeInvoiceStatus.Disputed },
      { label: this.translate.instant('pages.invoices.status_rejected'), value: EmployeeInvoiceStatus.Rejected },
      { label: this.translate.instant('pages.invoices.status_cancelled'), value: EmployeeInvoiceStatus.Cancelled },
    ];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.loadInvoices(offset, limit);
  }

  onSortChange(event: { field: string; order: number }): void {
    // Check if sort actually changed to prevent duplicate requests
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    // Update last sort state
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDef = [
      new SortDefinition({
        field: event.field,
        direction:
          event.order === 1
            ? SortDirection.Ascending
            : SortDirection.Descending,
      }),
    ];
    this.facade.updateSort(sortDef);
  }

  viewInvoiceDetails(invoice: EmployeeInvoice): void {
    this.router.navigate([CleansiaPartnerRoute.INVOICES, invoice.id]);
  }

  downloadInvoice(invoice: EmployeeInvoice): void {
    this.facade.downloadInvoice(invoice);
  }

  getStatusClass(invoice: EmployeeInvoice): string {
    const statusName = invoice.status.toLowerCase();
    return `status-badge status-${statusName}`;
  }

  // Filter methods
  applyFilters(): void {
    const formValues = this.searchForm.value;

    const filter = {
      invoiceNumber: formValues.invoiceNumber || undefined,
      minAmount: formValues.minAmount || undefined,
      maxAmount: formValues.maxAmount || undefined,
      dateFrom: formValues.dateFrom || undefined,
      dateTo: formValues.dateTo || undefined,
      statuses:
        formValues.statuses && formValues.statuses.length > 0
          ? formValues.statuses
          : undefined,
    };

    this.facade.applyFilters(filter);
  }

  resetFilters(): void {
    this.searchForm.reset();
    this.facade.resetFilters();
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  onInvoiceStatusChange(checked: boolean, statusValue: number): void {
    const currentStatuses = this.searchForm.get('statuses')?.value || [];
    if (checked) {
      this.searchForm.patchValue({
        statuses: [...currentStatuses, statusValue],
      });
    } else {
      this.searchForm.patchValue({
        statuses: currentStatuses.filter((s: number) => s !== statusValue),
      });
    }
  }

  getActiveFilterChips(): FilterChip[] {
    const chips: FilterChip[] = [];
    const formValue = this.searchForm.value;

    // Invoice number chip
    if (formValue.invoiceNumber) {
      chips.push({
        key: 'invoiceNumber',
        label: this.translate.instant('pages.invoices.filters.invoice_number'),
        value: formValue.invoiceNumber,
      });
    }

    // Date range chips
    if (formValue.dateFrom) {
      chips.push({
        key: 'dateFrom',
        label: this.translate.instant('pages.invoices.filters.date_from'),
        value: new Date(formValue.dateFrom).toLocaleDateString(),
      });
    }

    if (formValue.dateTo) {
      chips.push({
        key: 'dateTo',
        label: this.translate.instant('pages.invoices.filters.date_to'),
        value: new Date(formValue.dateTo).toLocaleDateString(),
      });
    }

    // Amount range chips
    if (formValue.minAmount != null) {
      chips.push({
        key: 'minAmount',
        label: this.translate.instant('pages.invoices.filters.min_amount'),
        value: formValue.minAmount.toString(),
      });
    }

    if (formValue.maxAmount != null) {
      chips.push({
        key: 'maxAmount',
        label: this.translate.instant('pages.invoices.filters.max_amount'),
        value: formValue.maxAmount.toString(),
      });
    }

    // Status chips (combined, matching orders pattern)
    if (formValue.statuses && formValue.statuses.length > 0) {
      const statusNames = formValue.statuses
        .map((id: number) => this.invoiceStatusOptions.find((o) => o.value === id)?.label)
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'statuses',
        label: this.translate.instant('pages.invoices.filters.invoice_status'),
        value: statusNames,
      });
    }

    return chips;
  }

  removeFilterChip(chipKey: string): void {
    if (chipKey === 'statuses') {
      // Reset all status checkboxes and the statuses array
      const resetValues: Record<string, any> = { statuses: [] };
      this.invoiceStatusOptions.forEach((opt) => {
        resetValues[`status_${opt.value}`] = false;
      });
      this.searchForm.patchValue(resetValues);
    } else {
      this.searchForm.patchValue({ [chipKey]: null });
    }
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  // Help card methods
  onHelpDismissedChange(): void {
    this.helpDismissedVersion.update(v => v + 1);
  }

  restoreHelp(): void {
    this.invoicesHelpCard()?.restore();
    this.helpDismissedVersion.update(v => v + 1);
  }
}
