import { CommonModule } from '@angular/common';
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
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { InvoiceManagementFacade } from './invoice-management.facade';
import {
  getInvoiceStatusClass,
  getInvoiceTableColumns,
  getInvoiceTableActions,
} from './invoice-management.models';

@Component({
  selector: 'cleansia-admin-invoice-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './invoice-management.component.html',
  providers: [InvoiceManagementFacade],
})
export class InvoiceManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(InvoiceManagementFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  invoiceTableColumns!: TableColumn<EmployeeInvoiceDto>[];
  invoiceTableActions!: TableAction<EmployeeInvoiceDto>[];

  readonly EmployeeInvoiceStatus = EmployeeInvoiceStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    status: [[] as EmployeeInvoiceStatus[]],
  });

  // Invoice status options - will be rebuilt on language change
  invoiceStatusMultiOptions: { label: string; value: EmployeeInvoiceStatus }[] = [];

  // Filter drawer state
  isFilterDrawerOpen = signal(false);
  // Signal to trigger recalculation of filter chips when form changes
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return this.getActiveFilterChips();
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Rebuild tables and filter options when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      });

    this.facade.loadInvoices();
  }

  private rebuildTableDefinitions(): void {
    this.invoiceTableColumns = getInvoiceTableColumns(
      this.translate,
      this.statusTemplate()
    );

    this.invoiceTableActions = getInvoiceTableActions(
      {
        onViewDetails: this.viewInvoiceDetails.bind(this),
        onDownload: this.downloadInvoice.bind(this),
      },
      this.translate
    );
  }

  private rebuildFilterOptions(): void {
    this.invoiceStatusMultiOptions = [
      {
        label: this.translate.instant('pages.invoice_management.invoice_status.pending'),
        value: EmployeeInvoiceStatus.Pending,
      },
      {
        label: this.translate.instant('pages.invoice_management.invoice_status.approved'),
        value: EmployeeInvoiceStatus.Approved,
      },
      {
        label: this.translate.instant('pages.invoice_management.invoice_status.paid'),
        value: EmployeeInvoiceStatus.Paid,
      },
      {
        label: this.translate.instant('pages.invoice_management.invoice_status.disputed'),
        value: EmployeeInvoiceStatus.Disputed,
      },
      {
        label: this.translate.instant('pages.invoice_management.invoice_status.rejected'),
        value: EmployeeInvoiceStatus.Rejected,
      },
      {
        label: this.translate.instant('pages.invoice_management.invoice_status.cancelled'),
        value: EmployeeInvoiceStatus.Cancelled,
      },
    ];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewInvoiceDetails(invoice: EmployeeInvoiceDto): void {
    this.router.navigate([CleansiaAdminRoute.INVOICE_MANAGEMENT, invoice.id]);
  }

  downloadInvoice(invoice: EmployeeInvoiceDto): void {
    this.facade.downloadInvoice(invoice);
  }

  getInvoiceStatusClass(invoice: EmployeeInvoiceDto): string {
    return getInvoiceStatusClass(invoice.status);
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      statuses:
        formValues.status && formValues.status.length > 0
          ? formValues.status
          : undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      status: [],
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: 1 | -1 }): void {
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    const sort = [
      new SortDefinition({
        field: event.field,
        direction: sortDirection,
      }),
    ];
    this.facade.onSortChange(sort);
  }

  onPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.onPageChange(offset, limit);
  }

  // Filter drawer methods
  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  getActiveFilterChips(): { key: string; label: string; value: string }[] {
    const chips: { key: string; label: string; value: string }[] = [];
    const values = this.filterForm.value;

    if (values.status && values.status.length > 0) {
      const statusLabels = values.status
        .map((s) => this.invoiceStatusMultiOptions.find((o) => o.value === s)?.label)
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'status',
        label: this.translate.instant('pages.invoice_management.filters.status'),
        value: statusLabels,
      });
    }

    return chips;
  }

  removeFilterChip(key: string): void {
    if (key === 'status') {
      this.filterForm.patchValue({ status: [] });
    }
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  // Checkbox helper methods for status
  isStatusChecked(status: EmployeeInvoiceStatus): boolean {
    return this.filterForm.value.status?.includes(status) ?? false;
  }

  toggleStatus(status: EmployeeInvoiceStatus): void {
    const isChecked = this.isStatusChecked(status);
    this.onStatusChange(status, !isChecked);
  }

  onStatusChange(status: EmployeeInvoiceStatus, checked: boolean): void {
    const currentStatuses = [...(this.filterForm.value.status || [])];

    if (checked) {
      if (!currentStatuses.includes(status)) {
        currentStatuses.push(status);
      }
    } else {
      const index = currentStatuses.indexOf(status);
      if (index > -1) {
        currentStatuses.splice(index, 1);
      }
    }

    this.filterForm.patchValue({ status: currentStatuses });
  }
}
