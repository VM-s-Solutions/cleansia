import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  ClosePayPeriodCommand,
  GetPagedPayPeriodsRequest,
  PayPeriodDto,
  PayPeriodFilter,
  PayPeriodStatus,
  SortDefinition,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';
import { PayPeriodFilterParams } from './pay-period-management.models';

@Injectable()
export class PayPeriodManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly payPeriods = signal<PayPeriodDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<PayPeriodFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly statusOptions = [
    {
      label: this.translate.instant('payPeriods.status.open'),
      value: PayPeriodStatus.Open,
    },
    {
      label: this.translate.instant('payPeriods.status.closed'),
      value: PayPeriodStatus.Closed,
    },
    {
      label: this.translate.instant('payPeriods.status.paid'),
      value: PayPeriodStatus.Paid,
    },
  ];

  loadPayPeriods(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    const payPeriodFilter = new PayPeriodFilter();
    if (filterParams?.status !== undefined) {
      payPeriodFilter.status = filterParams.status;
    }
    if (filterParams?.year !== undefined) {
      payPeriodFilter.year = filterParams.year;
    }

    const request = new GetPagedPayPeriodsRequest({
      offset: this.currentOffset(),
      limit: this.currentLimit(),
      filter: payPeriodFilter,
      sort: this.currentSort(),
    });

    this.adminClient.adminPayPeriodClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('payPeriods.messages.loadError')
          );
          console.error('Error loading pay periods:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payPeriods.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadPayPeriods();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadPayPeriods();
  }

  applyFilter(filter: PayPeriodFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPayPeriods();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadPayPeriods();
  }

  closePayPeriod(payPeriodId: string, notes?: string): void {
    const command = new ClosePayPeriodCommand({
      payPeriodId,
      notes,
    });

    this.adminClient.adminPayPeriodClient
      .close(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('payPeriods.messages.closeError')
          );
          console.error('Error closing pay period:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('payPeriods.messages.closeSuccess')
          );
          this.loadPayPeriods();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
