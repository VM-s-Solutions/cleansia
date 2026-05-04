import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminReferralListItem,
  ReferralStatus,
} from '@cleansia/admin-services';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export type ReferralStatusFilter = 'all' | 'accepted' | 'qualified' | 'expired';

export interface ReferralFilterParams {
  status?: ReferralStatusFilter;
  dateFrom?: Date;
  dateTo?: Date;
}

@Injectable()
export class ReferralsListFacade {
  private readonly adminClient = inject(AdminClient);

  private destroy$ = new Subject<void>();

  readonly referrals = signal<AdminReferralListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<ReferralFilterParams>({ status: 'all' });
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);

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
        takeUntil(this.destroy$),
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

  resetFilter(): void {
    this.currentFilter.set({ status: 'all' });
    this.currentOffset.set(0);
    this.loadReferrals();
  }

  /**
   * Maps the UI status filter to the backend ReferralStatus enum.
   * - all       => undefined (no filter)
   * - accepted  => ReferralStatus.Accepted
   * - qualified => ReferralStatus.Qualified
   * - expired   => ReferralStatus.Expired
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
      case 'all':
      default:
        return undefined;
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
