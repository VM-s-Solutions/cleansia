import { Injectable, inject, signal, computed } from '@angular/core';
import {
  AdminClient,
  RevenueReportDto,
  PayrollReportDto,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
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
          (currencies ?? [])
            .filter((c) => c.id && c.code)
            .map((c) => ({ id: c.id!, code: c.code!, isDefault: !!c.isDefault }))
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

  refreshCurrentReport(): void {
    if (this.activeTab() === 'revenue') {
      this.loadRevenueReport();
    } else {
      this.loadPayrollReport();
    }
  }

  resetToDefaultDateRange(): void {
    const defaultStart = this.getDefaultStartDate();
    const defaultEnd = new Date();
    this.setDateRange(defaultStart, defaultEnd);
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
  // "taken by this tender" column reconciles against a Stripe statement to the cent, and rounding
  // 45.10 € to 45 € is how lines stop summing.
  private formatAmount(value: number | undefined, currencyCode: string | undefined): string {
    if (value === undefined || value === null) return '';
    if (!currencyCode) return String(value);
    return new Intl.NumberFormat(this.translate.currentLang || 'en-GB', {
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