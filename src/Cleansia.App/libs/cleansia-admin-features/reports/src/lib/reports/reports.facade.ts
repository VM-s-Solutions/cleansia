import { Injectable, inject, signal, computed } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminClient,
  RevenueReportDto,
  PayrollReportDto,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage, formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export type ReportType = 'revenue' | 'payroll';

export interface DateRangeFilter {
  startDate: Date;
  endDate: Date;
}

@Injectable()
export class ReportsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly revenueReport = signal<RevenueReportDto | null>(null);
  readonly payrollReport = signal<PayrollReportDto | null>(null);
  readonly loadingRevenue = signal<boolean>(false);
  readonly loadingPayroll = signal<boolean>(false);

  readonly activeTab = signal<ReportType>('revenue');

  readonly currencies = signal<{ id: string; code: string; isDefault: boolean }[]>([]);
  /** undefined = let the server use the platform default. */
  readonly selectedCurrencyId = signal<string | undefined>(undefined);

  readonly dateRange = signal<DateRangeFilter>({
    startDate: this.getDefaultStartDate(),
    endDate: new Date(),
  });

  readonly defaultDateRange: DateRangeFilter = {
    startDate: this.getDefaultStartDate(),
    endDate: new Date(),
  };

  readonly isLoading = computed(
    () => this.loadingRevenue() || this.loadingPayroll()
  );

  readonly lang = currentLanguage(this.translate);

  readonly filterForm = inject(FormBuilder).group({
    startDate: [this.defaultDateRange.startDate as Date | null],
    endDate: [this.defaultDateRange.endDate as Date | null],
    currencyId: [null as string | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => this.buildFilterChips(value),
    apply: (value) => {
      if (value.startDate && value.endDate) {
        this.setDateRange(value.startDate, value.endDate, value.currencyId ?? undefined);
      }
    },
  });

  /** The headline is the server's net figure; the page derives no money arithmetic of its own. */
  readonly revenueHeadline = computed(() =>
    this.formatRevenueAmount(this.revenueReport()?.netRevenue)
  );

  readonly revenueBreakdown = computed(() => {
    const report = this.revenueReport();
    return {
      gross: this.formatRevenueAmount(report?.totalRevenue),
      refunded: this.formatRevenueAmount(report?.totalRefunded),
      credit: this.formatRevenueAmount(report?.totalReturnedToCredit),
    };
  });

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  // One chip for the range once either date leaves the default month, one for the currency.
  private buildFilterChips(value: {
    startDate?: Date | null;
    endDate?: Date | null;
    currencyId?: string | null;
  }): FilterChip[] {
    const chips: FilterChip[] = [];
    if (value.startDate && value.endDate) {
      const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();
      if (
        !sameDay(value.startDate, this.defaultDateRange.startDate) ||
        !sameDay(value.endDate, this.defaultDateRange.endDate)
      ) {
        const lang = this.lang();
        chips.push({
          key: 'dateRange',
          label: this.translate.instant('pages.reports.filters.date_range'),
          value: `${formatDate(value.startDate, lang)} - ${formatDate(value.endDate, lang)}`,
          controls: ['startDate', 'endDate'],
        });
      }
    }
    if (value.currencyId) {
      chips.push({
        key: 'currencyId',
        label: this.translate.instant('pages.reports.filters.currency'),
        value: this.currencies().find((c) => c.id === value.currencyId)?.code ?? '',
      });
    }
    return chips;
  }

  private getDefaultStartDate(): Date {
    const date = new Date();
    date.setMonth(date.getMonth() - 1);
    return date;
  }

  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(takeUntil(this.destroyed$), catchError(() => of([])))
      .subscribe((currencies) => {
        this.currencies.set(
          (currencies ?? []).flatMap((c) =>
            c.id && c.code ? [{ id: c.id, code: c.code, isDefault: !!c.isDefault }] : []
          )
        );
      });
  }

  loadRevenueReport(): void {
    this.loadingRevenue.set(true);
    const { startDate, endDate } = this.dateRange();

    this.adminClient.adminReportClient
      .revenue(startDate, endDate, this.selectedCurrencyId())
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loadingRevenue.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.revenueReport.set(response);
        }
      });
  }

  loadPayrollReport(): void {
    this.loadingPayroll.set(true);
    const { startDate, endDate } = this.dateRange();

    this.adminClient.adminReportClient
      .payroll(startDate, endDate, this.selectedCurrencyId())
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loadingPayroll.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payrollReport.set(response);
        }
      });
  }

  setActiveTab(tab: ReportType): void {
    this.activeTab.set(tab);
    if (tab === 'revenue' && !this.revenueReport()) {
      this.loadRevenueReport();
    } else if (tab === 'payroll' && !this.payrollReport()) {
      this.loadPayrollReport();
    }
  }

  setDateRange(startDate: Date, endDate: Date, currencyId?: string): void {
    this.selectedCurrencyId.set(currencyId);
    this.dateRange.set({ startDate, endDate });
    this.revenueReport.set(null);
    this.payrollReport.set(null);

    if (this.activeTab() === 'revenue') {
      this.loadRevenueReport();
    } else {
      this.loadPayrollReport();
    }
  }

  /** The revenue report's amounts, in the currency THAT report names. */
  formatRevenueAmount(value: number | undefined): string {
    return this.formatAmount(value, this.revenueReport()?.currencyCode);
  }

  /** The payroll report's amounts, in the currency THAT report names. */
  formatPayrollAmount(value: number | undefined): string {
    return this.formatAmount(value, this.payrollReport()?.currencyCode);
  }

  // The server names the currency; nothing here assumes one. No fraction-digit override: the
  // per-tender columns, net on tender above all, reconcile against a Stripe statement to the cent,
  // and rounding 45.10 € to 45 € is how lines stop summing.
  private formatAmount(value: number | undefined, currencyCode: string | undefined): string {
    if (value === undefined || value === null) return '';
    if (!currencyCode) return String(value);
    return new Intl.NumberFormat(this.lang() || 'en-GB', {
      style: 'currency',
      currency: currencyCode,
    }).format(value);
  }

  formatPercentage(value: number | undefined): string {
    if (value === undefined || value === null) return '0%';
    const sign = value >= 0 ? '+' : '';
    return `${sign}${value.toFixed(1)}%`;
  }
}