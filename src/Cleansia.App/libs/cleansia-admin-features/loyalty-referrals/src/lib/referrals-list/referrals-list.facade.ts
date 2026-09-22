import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminClient,
  AdminReferralListItem,
  ForceQualifyReferralCommand,
  ReferralStatus,
  ReverseReferralCommand,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage, formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export type ReferralStatusFilter =
  | 'all'
  | 'accepted'
  | 'qualified'
  | 'expired'
  | 'reversed';

export interface ReferralFilterParams {
  status?: ReferralStatusFilter;
  dateFrom?: Date;
  dateTo?: Date;
}

const STATUS_FILTER_LABEL_KEYS: Readonly<Record<ReferralStatusFilter, string>> = {
  all: 'pages.loyalty_referrals.filter.status_all',
  accepted: 'pages.loyalty_referrals.filter.status_accepted',
  qualified: 'pages.loyalty_referrals.filter.status_qualified',
  expired: 'pages.loyalty_referrals.filter.status_expired',
  reversed: 'pages.loyalty_referrals.filter.status_reversed',
};

@Injectable()
export class ReferralsListFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly referrals = signal<AdminReferralListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly intervening = signal<boolean>(false);

  readonly lang = currentLanguage(this.translate);
  readonly statusFilterOptions = computed<ICleansiaSelectOption[]>(() => {
    this.lang();
    return (Object.keys(STATUS_FILTER_LABEL_KEYS) as ReferralStatusFilter[]).map((value) => ({
      label: this.translate.instant(STATUS_FILTER_LABEL_KEYS[value]),
      value,
    }));
  });
  readonly filterForm = inject(FormBuilder).group({
    status: ['all' as ReferralStatusFilter],
    dateFrom: [null as Date | null],
    dateTo: [null as Date | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.status && value.status !== 'all'
        ? [
            {
              key: 'status',
              label: this.translate.instant('pages.loyalty_referrals.filter.status'),
              value: this.translate.instant(STATUS_FILTER_LABEL_KEYS[value.status]),
            },
          ]
        : []),
      ...(value.dateFrom || value.dateTo
        ? [
            {
              key: 'dateRange',
              label: this.translate.instant('pages.loyalty_referrals.filter.date_range'),
              value: [value.dateFrom, value.dateTo]
                .map((date) => formatDate(date, this.translate.currentLang))
                .join(' – '),
              controls: ['dateFrom', 'dateTo'],
            },
          ]
        : []),
    ],
    apply: (value) =>
      this.applyFilter({
        status: value.status ?? 'all',
        dateFrom: value.dateFrom ?? undefined,
        dateTo: value.dateTo ?? undefined,
      }),
  });

  private currentFilter = signal<ReferralFilterParams>({ status: 'all' });
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadReferrals(): void {
    this.loading.set(true);
    const filter = this.currentFilter();
    const status = this.toServerStatus(filter.status);

    this.adminClient.adminReferralClient
      .getPaged(
        status,
        filter.dateFrom,
        filter.dateTo,
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
          this.referrals.set(response.data ?? []);
          this.totalRecords.set(response.total ?? 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadReferrals();
  }

  applyFilter(filter: ReferralFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadReferrals();
  }

  reverseReferral(
    referralId: string,
    reason: string,
    onSuccess?: () => void
  ): void {
    const trimmed = reason.trim();
    if (!referralId || !trimmed || this.intervening()) return;

    this.intervening.set(true);
    const command = new ReverseReferralCommand();
    command.referralId = referralId;
    command.reason = trimmed;

    this.adminClient.adminReferralClient
      .reverse(referralId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.intervening.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(
            'pages.loyalty_referrals.intervention.success_reverse'
          );
          this.loadReferrals();
          onSuccess?.();
        }
      });
  }

  forceQualifyReferral(
    referralId: string,
    reason: string,
    onSuccess?: () => void
  ): void {
    const trimmed = reason.trim();
    if (!referralId || !trimmed || this.intervening()) return;

    this.intervening.set(true);
    const command = new ForceQualifyReferralCommand();
    command.referralId = referralId;
    command.reason = trimmed;

    this.adminClient.adminReferralClient
      .forceQualify(referralId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.intervening.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(
            'pages.loyalty_referrals.intervention.success_force_qualify'
          );
          this.loadReferrals();
          onSuccess?.();
        }
      });
  }

  /**
   * Maps the UI status filter to the backend ReferralStatus enum.
   * - all       => undefined (no filter)
   * - accepted  => ReferralStatus.Accepted
   * - qualified => ReferralStatus.Qualified
   * - expired   => ReferralStatus.Expired
   * - reversed  => ReferralStatus.Reversed
   */
  private toServerStatus(
    status: ReferralStatusFilter | undefined
  ): ReferralStatus | undefined {
    switch (status) {
      case 'accepted':
        return ReferralStatus.Accepted;
      case 'qualified':
        return ReferralStatus.Qualified;
      case 'expired':
        return ReferralStatus.Expired;
      case 'reversed':
        return ReferralStatus.Reversed;
      case 'all':
      default:
        return undefined;
    }
  }
}
