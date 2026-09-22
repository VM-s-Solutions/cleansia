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
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { PeriodCurrency, getPeriodCurrencies } from './period-pay.models';

const PERIODS_LIMIT = 26;

@Injectable()
export class PeriodPayFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);

  readonly lang = currentLanguage(this.translate);
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
    this.loadSummary();
  }

  selectCurrency(currencyId: string): void {
    this.selectedCurrencyId.set(currencyId);
    this.currencyControl?.setValue(currencyId, { emitEvent: false });
    this.loadSummary();
  }

  retry(): void {
    if (!this.employeeId || this.payPeriods().length === 0) {
      this.init();
      return;
    }
    this.loadSummary();
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
   * Unnamed, the currency view is the server's choice (the cleaner's resolved currency, or the live
   * invoice's), so the first load asks for nothing; the summary then names the currencies the period
   * can be viewed in, with the one it is shown in first, and the switch is set from that answer.
   */
  private loadSummary(): void {
    const payPeriodId = this.selectedPeriodId();
    if (!this.employeeId || !payPeriodId) {
      return;
    }

    this.loading.set(true);
    this.hasError.set(false);

    this.partnerClient.employeePayrollClient
      .getPeriodPays(this.employeeId, payPeriodId, this.selectedCurrencyId() ?? undefined)
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
      .subscribe((summary) => {
        if (!summary) {
          return;
        }
        const currencies = getPeriodCurrencies(summary);
        this.summary.set(summary);
        this.periodCurrencies.set(currencies);
        const shownCurrencyId =
          currencies.find((currency) => currency.code === summary.currencyCode)?.id ?? null;
        this.selectedCurrencyId.set(shownCurrencyId);
        this.currencyControl?.setValue(shownCurrencyId, { emitEvent: false });
      });
  }
}
