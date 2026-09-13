import { Injectable, inject, signal } from '@angular/core';
import {
  AdminMembershipClient,
  MembershipPlanListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { resolveMembershipPlanErrorKey } from './membership-plan-list.models';

export interface MembershipPlanFilterParams {
  active?: boolean;
  search?: string;
}

@Injectable()
export class MembershipPlanListFacade extends UnsubscribeControlDirective {
  private readonly membershipClient = inject(AdminMembershipClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly plans = signal<MembershipPlanListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);
  readonly deactivating = signal<boolean>(false);

  private readonly currentFilter = signal<MembershipPlanFilterParams | null>(
    null
  );
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);

  loadPlans(): void {
    this.loading.set(true);
    this.hasError.set(false);
    const filter = this.currentFilter();

    this.membershipClient
      .getPaged(
        filter?.active,
        filter?.search,
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.plans.set(response.data ?? []);
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
    this.loadPlans();
  }

  applyFilter(filter: MembershipPlanFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPlans();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadPlans();
  }

  deactivatePlan(row: MembershipPlanListItem): void {
    if (!row.id || this.deactivating()) return;

    this.deactivating.set(true);
    this.membershipClient
      .deactivate(row.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(resolveMembershipPlanErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.deactivating.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant(
              'pages.membership_plans.messages.deactivate_success'
            )
          );
          this.loadPlans();
        }
      });
  }
}
