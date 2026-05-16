import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
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
  PayPeriodDto,
  PayPeriodStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLoaderComponent,
  CleansiaRadioComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { PayPeriodManagementFacade } from './pay-period-management.facade';
import {
  getPayPeriodTableColumns,
  getPayPeriodTableActions,
} from './pay-period-management.models';

@Component({
  selector: 'cleansia-admin-pay-period-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaRadioComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
  ],
  templateUrl: './pay-period-management.component.html',
  providers: [PayPeriodManagementFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PayPeriodManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(PayPeriodManagementFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  payPeriodTableColumns!: TableColumn<PayPeriodDto>[];
  payPeriodTableActions!: TableAction<PayPeriodDto>[];

  // Expose PayPeriodStatus enum to template
  readonly PayPeriodStatus = PayPeriodStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  // Filter form
  filterForm = this.fb.group({
    status: [null as number | null],
    year: [null as number | null],
  });

  // Year options - generate last 5 years
  yearOptions = Array.from({ length: 5 }, (_, i) => {
    const year = new Date().getFullYear() - i;
    return { label: year.toString(), value: year };
  });

  // Status options - will be rebuilt on language change
  statusOptions: { label: string; value: PayPeriodStatus }[] = [];

  // Create pay period dialog state
  showCreateDialog = signal(false);
  createStartDate = signal<Date | null>(null);
  createEndDate = signal<Date | null>(null);

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

    // Update filter chips immediately when form changes
    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    // Setup automatic filtering with debounce
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

    // Load pay periods on init
    this.facade.loadPayPeriods();
  }

  private rebuildTableDefinitions(): void {
    this.payPeriodTableColumns = getPayPeriodTableColumns(
      this.translate,
      this.statusTemplate()
    );

    this.payPeriodTableActions = getPayPeriodTableActions(
      {
        onViewDetails: this.viewPayPeriodDetails.bind(this),
        onClose: this.closePayPeriod.bind(this),
      },
      this.translate
    );
  }

  private rebuildFilterOptions(): void {
    this.statusOptions = [
      {
        label: this.translate.instant('pay_periods.status.open'),
        value: PayPeriodStatus.Open,
      },
      {
        label: this.translate.instant('pay_periods.status.closed'),
        value: PayPeriodStatus.Closed,
      },
      {
        label: this.translate.instant('pay_periods.status.paid'),
        value: PayPeriodStatus.Paid,
      },
    ];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewPayPeriodDetails(payPeriod: PayPeriodDto): void {
    this.router.navigate([CleansiaAdminRoute.PAY_PERIODS, payPeriod.id]);
  }

  closePayPeriod(payPeriod: PayPeriodDto): void {
    // TODO: Open dialog for confirmation and notes
    if (confirm('Are you sure you want to close this pay period?')) {
      this.facade.closePayPeriod(payPeriod.id!);
    }
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      status: formValues.status ?? undefined,
      year: formValues.year ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      status: null,
      year: null,
    });
    this.facade.resetFilter();
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

    if (values.status !== null && values.status !== undefined) {
      const statusOption = this.statusOptions.find(
        (opt) => opt.value === values.status
      );
      chips.push({
        key: 'status',
        label: this.translate.instant('pay_periods.filters.status'),
        value: statusOption?.label || values.status.toString(),
      });
    }

    if (values.year !== null && values.year !== undefined) {
      chips.push({
        key: 'year',
        label: this.translate.instant('pay_periods.filters.year'),
        value: values.year.toString(),
      });
    }

    return chips;
  }

  removeFilterChip(key: string): void {
    this.filterForm.patchValue({ [key]: null });
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  // Radio helper methods
  onStatusSelect(value: number | null): void {
    this.filterForm.patchValue({ status: value });
  }

  onYearSelect(value: number | null): void {
    this.filterForm.patchValue({ year: value });
  }

  getPayPeriodStatusLabel(payPeriod: PayPeriodDto): string {
    if (!payPeriod.status) return '';
    const statusKey = payPeriod.status.toLowerCase();
    return this.translate.instant(`pay_periods.status.${statusKey}`);
  }

  openCreateDialog(): void {
    this.showCreateDialog.set(true);
    this.createStartDate.set(null);
    this.createEndDate.set(null);
  }

  closeCreateDialog(): void {
    this.showCreateDialog.set(false);
  }

  createPayPeriod(): void {
    const startDate = this.createStartDate();
    const endDate = this.createEndDate();
    if (!startDate || !endDate) return;

    // TODO: Wire up to admin client create pay period endpoint once available
    console.warn('Create pay period not yet wired to backend', { startDate, endDate });
    this.closeCreateDialog();
  }

  getPayPeriodStatusClass(payPeriod: PayPeriodDto): string {
    if (!payPeriod.status) return 'pay-period-status-badge status-open';
    const statusKey = payPeriod.status.toLowerCase();
    return `pay-period-status-badge status-${statusKey}`;
  }
}
