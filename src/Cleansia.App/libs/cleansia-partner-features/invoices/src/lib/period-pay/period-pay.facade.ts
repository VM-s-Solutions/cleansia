import { Injectable, computed, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  PartnerClient,
  PayPeriodDto,
  PeriodPaySummaryDto,
  SortDefinition,
  SortDirection,
} from '@cleansia/partner-services';
import { catchError, finalize, forkJoin, map, of, takeUntil } from 'rxjs';
import { PeriodCurrency, PeriodStatusKey, getPeriodCurrencies } from './period-pay.models';

const PERIODS_LIMIT = 26;
const PERIOD_INVOICES_LIMIT = 20;

@Injectable()
export class PeriodPayFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);

  readonly payPeriods = signal<PayPeriodDto[]>([]);
  readonly summary = signal<PeriodPaySummaryDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);
  readonly selectedPeriodId = signal<string | null>(null);
  readonly periodCurrencies = signal<PeriodCurrency[]>([]);
  readonly selectedCurrencyId = signal<string | null>(null);

  readonly periodOptions = computed<ICleansiaSelectOption[]>(() =>
    this.payPeriods()
      .filter((period) => !!period.id)
      .map((period) => ({ label: period.periodLabel ?? '', value: period.id }))
  );

  readonly currencyOptions = computed<ICleansiaSelectOption[]>(() =>
    this.periodCurrencies().map((currency) => ({ label: currency.code, value: currency.id }))
  );

  readonly hasMultipleCurrencies = computed<boolean>(() => this.periodCurrencies().length > 1);

  readonly selectedPeriod = computed<PayPeriodDto | null>(
    () =>
      this.payPeriods().find((period) => period.id === this.selectedPeriodId()) ?? null
  );

  readonly selectedPeriodStatus = computed<PeriodStatusKey>(() => {
    switch (this.selectedPeriod()?.status?.toLowerCase()) {
      case 'open':
        return 'open';
      case 'closed':
        return 'closed';
      case 'paid':
        return 'paid';
      default:
        return 'unknown';
    }
  });

  private employeeId: string | null = null;
  private periodControl: FormControl<string | null> | null = null;
  private currencyControl: FormControl<string | null> | null = null;

  connectPeriodControl(control: FormControl<string | null>): void {
    this.periodControl = control;
    control.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe((periodId) => {
        if (periodId && periodId !== this.selectedPeriodId()) {
          this.selectPeriod(periodId);
        }
      });
  }

  connectCurrencyControl(control: FormControl<string | null>): void {
    this.currencyControl = control;
    control.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe((currencyId) => {
        if (currencyId && currencyId !== this.selectedCurrencyId()) {
          this.selectCurrency(currencyId);
        }
      });
  }

  init(): void {
    this.initialLoading.set(true);
    this.hasError.set(false);

    this.partnerClient.employeeClient
      .getCurrentEmployee()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        })
      )
      .subscribe((employee) => {
        if (employee?.id) {
          this.employeeId = employee.id;
          this.loadPayPeriods();
        } else {
          this.initialLoading.set(false);
        }
      });
  }

  selectPeriod(payPeriodId: string): void {
    this.selectedPeriodId.set(payPeriodId);
    this.periodControl?.setValue(payPeriodId, { emitEvent: false });
    this.selectedCurrencyId.set(null);
    this.currencyControl?.setValue(null, { emitEvent: false });
    this.loadSummary(true);
  }

  selectCurrency(currencyId: string): void {
    this.selectedCurrencyId.set(currencyId);
    this.currencyControl?.setValue(currencyId, { emitEvent: false });
    this.loadSummary(false);
  }

  retry(): void {
    if (!this.employeeId || this.payPeriods().length === 0) {
      this.init();
      return;
    }
    this.loadSummary(true);
  }

  private loadPayPeriods(): void {
    this.partnerClient.payPeriodClient
      .getPagedPayPeriods(
        undefined,
        undefined,
        [
          new SortDefinition({
            field: 'startDate',
            direction: SortDirection.Descending,
          }),
        ],
        0,
        PERIODS_LIMIT
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          this.initialLoading.set(false);
          return of(null);
        })
      )
      .subscribe((paged) => {
        if (!paged) {
          return;
        }
        const periods = paged.data ?? [];
        this.payPeriods.set(periods);
        const latestPeriodId = periods[0]?.id;
        if (latestPeriodId) {
          this.selectPeriod(latestPeriodId);
        } else {
          this.initialLoading.set(false);
        }
      });
  }

  /**
   * Unnamed, the currency view is the server's choice (the cleaner's resolved currency, or the one
   * invoice's), so the first load asks for nothing and the switch is then set to whatever came back.
   */
  private loadSummary(withCurrencies: boolean): void {
    const payPeriodId = this.selectedPeriodId();
    if (!this.employeeId || !payPeriodId) {
      return;
    }

    this.loading.set(true);
    this.hasError.set(false);

    const payrollClient = this.partnerClient.employeePayrollClient;
    const currencies$ = withCurrencies
      ? payrollClient
          .getPagedInvoices(
            this.employeeId,
            payPeriodId,
            undefined,
            undefined,
            undefined,
            undefined,
            undefined,
            undefined,
            undefined,
            undefined,
            0,
            PERIOD_INVOICES_LIMIT
          )
          .pipe(map((paged) => getPeriodCurrencies(paged.data)))
      : of(this.periodCurrencies());

    forkJoin({
      summary: payrollClient.getPeriodPays(
        this.employeeId,
        payPeriodId,
        this.selectedCurrencyId() ?? undefined
      ),
      currencies: currencies$,
    })
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => {
          this.loading.set(false);
          if (this.initialLoading()) {
            this.initialLoading.set(false);
          }
        })
      )
      .subscribe((view) => {
        if (!view) {
          return;
        }
        this.summary.set(view.summary);
        this.periodCurrencies.set(view.currencies);
        const shownCurrencyId =
          this.selectedCurrencyId() ??
          view.currencies.find((currency) => currency.code === view.summary.currencyCode)?.id ??
          null;
        this.selectedCurrencyId.set(shownCurrencyId);
        this.currencyControl?.setValue(shownCurrencyId, { emitEvent: false });
      });
  }
}
