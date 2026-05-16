import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
} from '@cleansia/components';
import {
  EmployeePayrollSummary,
  MonthlyPayroll,
  PayrollByStatus,
  RevenueByPackage,
  RevenueByPaymentType,
  RevenueByService,
} from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { TranslatePipe } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { Tabs } from 'primeng/tabs';
import { TabList } from 'primeng/tabs';
import { Tab } from 'primeng/tabs';
import { TabPanels } from 'primeng/tabs';
import { TabPanel } from 'primeng/tabs';
import { ReportsFacade, ReportType } from './reports.facade';

@Component({
  selector: 'cleansia-admin-reports',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './reports.component.html',
  providers: [ReportsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly cd = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(ReportsFacade);

  dateRangeForm = this.fb.group({
    startDate: [this.facade.dateRange().startDate],
    endDate: [this.facade.dateRange().endDate],
  });

  // Filter drawer state
  isFilterDrawerOpen = signal(false);
  activeFilterChips = computed(() => this.getActiveFilterChips());
  hasActiveFilters = computed(() => {
    // Check if current dates differ from default dates
    const values = this.dateRangeForm.value;
    const defaultRange = this.facade.defaultDateRange;
    if (!values.startDate || !values.endDate) return false;

    const startDiffers = values.startDate.toDateString() !== defaultRange.startDate.toDateString();
    const endDiffers = values.endDate.toDateString() !== defaultRange.endDate.toDateString();
    return startDiffers || endDiffers;
  });
  activeFilterCount = computed(() => this.hasActiveFilters() ? 1 : 0);

  // Revenue Tables
  revenueByServiceColumns: TableColumn<RevenueByService>[] = [];
  revenueByPackageColumns: TableColumn<RevenueByPackage>[] = [];
  revenueByPaymentTypeColumns: TableColumn<RevenueByPaymentType>[] = [];

  // Payroll Tables
  employeeSummariesColumns: TableColumn<EmployeePayrollSummary>[] = [];
  payrollByStatusColumns: TableColumn<PayrollByStatus>[] = [];
  monthlyPayrollColumns: TableColumn<MonthlyPayroll>[] = [];

  ngOnInit(): void {
    this.rebuildTableDefinitions();
    this.facade.loadRevenueReport();
    this.setupAutoFilter();

    // Rebuild tables when language changes
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.cd.detectChanges();
      });
  }

  private rebuildTableDefinitions(): void {
    // Revenue Tables
    this.revenueByServiceColumns = [
      {
        id: 'serviceName',
        field: 'serviceName',
        header: this.translate.instant('pages.reports.service_name'),
      },
      {
        id: 'orderCount',
        field: 'orderCount',
        header: this.translate.instant('pages.reports.order_count'),
      },
      {
        id: 'totalRevenue',
        field: 'totalRevenue',
        header: this.translate.instant('pages.reports.total_revenue'),
        getValue: (row) => this.facade.formatCurrency(row?.totalRevenue),
      },
    ];

    this.revenueByPackageColumns = [
      {
        id: 'packageName',
        field: 'packageName',
        header: this.translate.instant('pages.reports.package_name'),
      },
      {
        id: 'orderCount',
        field: 'orderCount',
        header: this.translate.instant('pages.reports.order_count'),
      },
      {
        id: 'totalRevenue',
        field: 'totalRevenue',
        header: this.translate.instant('pages.reports.total_revenue'),
        getValue: (row) => this.facade.formatCurrency(row?.totalRevenue),
      },
    ];

    this.revenueByPaymentTypeColumns = [
      {
        id: 'paymentTypeName',
        field: 'paymentTypeName',
        header: this.translate.instant('pages.reports.payment_type'),
      },
      {
        id: 'orderCount',
        field: 'orderCount',
        header: this.translate.instant('pages.reports.order_count'),
      },
      {
        id: 'totalRevenue',
        field: 'totalRevenue',
        header: this.translate.instant('pages.reports.total_revenue'),
        getValue: (row) => this.facade.formatCurrency(row?.totalRevenue),
      },
    ];

    // Payroll Tables
    this.employeeSummariesColumns = [
      {
        id: 'employeeName',
        field: 'employeeName',
        header: this.translate.instant('pages.reports.employee_name'),
      },
      {
        id: 'totalOrders',
        field: 'totalOrders',
        header: this.translate.instant('pages.reports.total_orders'),
      },
      {
        id: 'invoiceCount',
        field: 'invoiceCount',
        header: this.translate.instant('pages.reports.invoice_count'),
      },
      {
        id: 'subTotal',
        field: 'subTotal',
        header: this.translate.instant('pages.reports.subtotal'),
        getValue: (row) => this.facade.formatCurrency(row?.subTotal),
      },
      {
        id: 'bonusAmount',
        field: 'bonusAmount',
        header: this.translate.instant('pages.reports.bonus'),
        getValue: (row) => this.facade.formatCurrency(row?.bonusAmount),
      },
      {
        id: 'deductionAmount',
        field: 'deductionAmount',
        header: this.translate.instant('pages.reports.deductions'),
        getValue: (row) => this.facade.formatCurrency(row?.deductionAmount),
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: this.translate.instant('pages.reports.total_amount'),
        getValue: (row) => this.facade.formatCurrency(row?.totalAmount),
      },
    ];

    this.payrollByStatusColumns = [
      {
        id: 'statusName',
        field: 'statusName',
        header: this.translate.instant('pages.reports.status'),
      },
      {
        id: 'invoiceCount',
        field: 'invoiceCount',
        header: this.translate.instant('pages.reports.invoice_count'),
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: this.translate.instant('pages.reports.total_amount'),
        getValue: (row) => this.facade.formatCurrency(row?.totalAmount),
      },
    ];

    this.monthlyPayrollColumns = [
      {
        id: 'month',
        field: 'monthName',
        header: this.translate.instant('pages.reports.month'),
        getValue: (row) => `${row?.monthName} ${row?.year}`,
      },
      {
        id: 'invoiceCount',
        field: 'invoiceCount',
        header: this.translate.instant('pages.reports.invoice_count'),
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: this.translate.instant('pages.reports.total_amount'),
        getValue: (row) => this.facade.formatCurrency(row?.totalAmount),
      },
    ];
  }

  private setupAutoFilter(): void {
    this.dateRangeForm.valueChanges
      .pipe(
        debounceTime(500),
        distinctUntilChanged(
          (prev, curr) =>
            prev.startDate?.getTime() === curr.startDate?.getTime() &&
            prev.endDate?.getTime() === curr.endDate?.getTime()
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((value) => {
        if (value.startDate && value.endDate) {
          this.facade.setDateRange(value.startDate, value.endDate);
        }
      });
  }

  activeTab: ReportType = 'revenue';

  onTabChange(value: string | number): void {
    const tab = value as ReportType;
    this.activeTab = tab;
    this.facade.setActiveTab(tab);
  }

  refreshReport(): void {
    this.facade.refreshCurrentReport();
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
    const values = this.dateRangeForm.value;
    const defaultRange = this.facade.defaultDateRange;

    if (!values.startDate || !values.endDate) return chips;

    const startDiffers = values.startDate.toDateString() !== defaultRange.startDate.toDateString();
    const endDiffers = values.endDate.toDateString() !== defaultRange.endDate.toDateString();

    // Only show a single "Date Range" chip if either date differs from default
    if (startDiffers || endDiffers) {
      chips.push({
        key: 'dateRange',
        label: this.translate.instant('pages.reports.filters.date_range'),
        value: `${this.formatDate(values.startDate)} - ${this.formatDate(values.endDate)}`,
      });
    }

    return chips;
  }

  private formatDate(date: Date): string {
    return date.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  }

  removeFilterChip(key: string): void {
    // For reports, removing the date range chip resets to defaults
    if (key === 'dateRange') {
      this.resetFilters();
    }
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  resetFilters(): void {
    const defaultRange = this.facade.defaultDateRange;
    // Create fresh dates to avoid reference issues
    const defaultStart = new Date(defaultRange.startDate);
    const defaultEnd = new Date(defaultRange.endDate);
    this.dateRangeForm.patchValue({
      startDate: defaultStart,
      endDate: defaultEnd,
    });
    this.facade.resetToDefaultDateRange();
  }
}
