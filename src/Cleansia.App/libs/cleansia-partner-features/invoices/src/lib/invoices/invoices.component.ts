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
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { SortDefinition, SortDirection } from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { EmployeeInvoice, InvoicesFacade } from './invoices.facade';
import {
  buildFilterChips,
  buildInvoiceStatusOptions,
  getInvoiceStatusClass,
  INVOICES_HELP_STEPS,
  INVOICE_STATUS_FLOW,
} from './invoices.helpers';
import { getInvoicesTableDefinition } from './invoices.models';

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
    invoiceNumber: [''],
    minAmount: [null as number | null],
    maxAmount: [null as number | null],
    dateFrom: [null as Date | null],
    dateTo: [null as Date | null],
    status_1: [false], // Pending
    status_2: [false], // Approved
    status_3: [false], // Paid
    status_4: [false], // Disputed
    status_5: [false], // Rejected
    status_6: [false], // Cancelled
    statuses: [[] as number[]],
  });

  // Extracted constants and builders
  invoiceStatusOptions = buildInvoiceStatusOptions(this.translate);
  invoicesHelpSteps = INVOICES_HELP_STEPS;
  invoiceStatusFlow = INVOICE_STATUS_FLOW;

  // Filter reactivity
  private filterFormVersion = signal(0);

  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return buildFilterChips(this.searchForm.value, this.invoiceStatusOptions, this.translate);
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    this.searchForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    this.searchForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

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
      { onDownload: this.downloadInvoice.bind(this) },
      this.statusTemplate()
    );
    this.invoicesColumns = def.columns;
    this.invoicesActions = def.actions;
  }

  private rebuildFilterOptions(): void {
    this.invoiceStatusOptions = buildInvoiceStatusOptions(this.translate);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(event: PaginationState): void {
    this.facade.loadInvoices(event.first, event.rows);
  }

  onSortChange(event: { field: string; order: number }): void {
    if (event.field === this.lastSortField && event.order === this.lastSortOrder) {
      return;
    }
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    this.facade.updateSort([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
  }

  viewInvoiceDetails(invoice: EmployeeInvoice): void {
    this.router.navigate([CleansiaPartnerRoute.INVOICES, invoice.id]);
  }

  downloadInvoice(invoice: EmployeeInvoice): void {
    this.facade.downloadInvoice(invoice);
  }

  getStatusClass(invoice: EmployeeInvoice): string {
    return getInvoiceStatusClass(invoice);
  }

  applyFilters(): void {
    const f = this.searchForm.value;
    this.facade.applyFilters({
      invoiceNumber: f.invoiceNumber || undefined,
      minAmount: f.minAmount || undefined,
      maxAmount: f.maxAmount || undefined,
      dateFrom: f.dateFrom || undefined,
      dateTo: f.dateTo || undefined,
      statuses: f.statuses && f.statuses.length > 0 ? f.statuses : undefined,
    });
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
    this.searchForm.patchValue({
      statuses: checked
        ? [...currentStatuses, statusValue]
        : currentStatuses.filter((s: number) => s !== statusValue),
    });
  }

  removeFilterChip(chipKey: string): void {
    if (chipKey === 'statuses') {
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

  onHelpDismissedChange(): void {
    this.helpDismissedVersion.update(v => v + 1);
  }

  restoreHelp(): void {
    this.invoicesHelpCard()?.restore();
    this.helpDismissedVersion.update(v => v + 1);
  }
}
