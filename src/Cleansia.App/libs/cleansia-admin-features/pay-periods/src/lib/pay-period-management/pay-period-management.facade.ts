import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  ClosePayPeriodCommand,
  PayPeriodDto,
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
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<PayPeriodFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly statusOptions = [
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

  loadPayPeriods(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminPayPeriodClient
      .getPaged(
        filterParams?.status,
        filterParams?.year,
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payPeriods.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
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
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pay_periods.messages.close_success')
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
