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
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { PeriodStatusKey } from './period-pay.models';

const PERIODS_LIMIT = 26;

@Injectable()
export class PeriodPayFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);

  readonly payPeriods = signal<PayPeriodDto[]>([]);
  readonly summary = signal<PeriodPaySummaryDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);
  readonly selectedPeriodId = signal<string | null>(null);

  readonly periodOptions = computed<ICleansiaSelectOption[]>(() =>
    this.payPeriods()
      .filter((period) => !!period.id)
      .map((period) => ({ label: period.periodLabel ?? '', value: period.id }))
  );

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

  private loadSummary(): void {
    const payPeriodId = this.selectedPeriodId();
    if (!this.employeeId || !payPeriodId) {
      return;
    }

    this.loading.set(true);
    this.hasError.set(false);

    this.partnerClient.employeePayrollClient
      .getPeriodPays(this.employeeId, payPeriodId)
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
        if (summary) {
          this.summary.set(summary);
        }
      });
  }
}
