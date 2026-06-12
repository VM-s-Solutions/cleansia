import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminReferralListItem,
  ForceQualifyReferralCommand,
  ReferralStatus,
  ReverseReferralCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { resolveReferralErrorKey } from './referrals-list.models';

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

  resetFilter(): void {
    this.currentFilter.set({ status: 'all' });
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
    const command = new ReverseReferralCommand({ referralId, reason: trimmed });

    this.adminClient.adminReferralClient
      .reverse(referralId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(resolveReferralErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.intervening.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant(
              'pages.loyalty_referrals.intervention.success_reverse'
            )
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
    const command = new ForceQualifyReferralCommand({
      referralId,
      reason: trimmed,
    });

    this.adminClient.adminReferralClient
      .forceQualify(referralId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(resolveReferralErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.intervening.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant(
              'pages.loyalty_referrals.intervention.success_force_qualify'
            )
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
