import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminClient,
  ClosePayPeriodCommand,
  PayPeriodDto,
  PayPeriodStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { PayPeriodFilterParams } from './pay-period-management.models';

@Injectable()
export class PayPeriodManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly payPeriods = signal<PayPeriodDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
  readonly statusOptions = computed(() => {
    this.lang();
    return [PayPeriodStatus.Open, PayPeriodStatus.Closed, PayPeriodStatus.Paid].map((status) => ({
      label: this.translate.instant(`pay_periods.status.${PayPeriodStatus[status].toLowerCase()}`),
      value: status,
    }));
  });
  readonly yearOptions = Array.from({ length: 5 }, (_, i) => {
    const year = new Date().getFullYear() - i;
    return { label: year.toString(), value: year };
  });
  readonly filterForm = inject(FormBuilder).group({
    status: [null as PayPeriodStatus | null],
    year: [null as number | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.status !== null && value.status !== undefined
        ? [
            {
              key: 'status',
              label: this.translate.instant('pay_periods.filters.status'),
              value:
                this.statusOptions().find((option) => option.value === value.status)?.label ??
                String(value.status),
            },
          ]
        : []),
      ...(value.year !== null && value.year !== undefined
        ? [
            {
              key: 'year',
              label: this.translate.instant('pay_periods.filters.year'),
              value: String(value.year),
            },
          ]
        : []),
    ],
    apply: (value) =>
      this.applyFilter({ status: value.status ?? undefined, year: value.year ?? undefined }),
  });

  private currentFilter = signal<PayPeriodFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

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
        takeUntil(this.destroyed$),
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

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadPayPeriods();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadPayPeriods();
  }

  applyFilter(filter: PayPeriodFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPayPeriods();
  }

  selectStatus(value: PayPeriodStatus | null): void {
    this.filterForm.patchValue({ status: value });
  }

  selectYear(value: number | null): void {
    this.filterForm.patchValue({ year: value });
  }

  closePayPeriod(payPeriodId: string, notes?: string): void {
    const command = new ClosePayPeriodCommand();
    command.payPeriodId = payPeriodId;
    command.notes = notes;

    this.adminClient.adminPayPeriodClient
      .close(command)
      .pipe(
        takeUntil(this.destroyed$),
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
}
