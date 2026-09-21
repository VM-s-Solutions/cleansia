import { Injectable, inject, signal } from '@angular/core';
import {
  AdminMembershipClient,
  MembershipPlanListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

export interface MembershipPlanFilterParams {
  active?: boolean;
  search?: string;
}

@Injectable()
export class MembershipPlanListFacade extends UnsubscribeControlDirective {
  private readonly membershipClient = inject(AdminMembershipClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbar = inject(SnackbarService);

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
    const id = row.id;
    if (!id || this.deactivating()) return;

    this.dialog
      .confirmTranslated(
        'pages.membership_plans.deactivate_confirm.message',
        'pages.membership_plans.deactivate_confirm.title',
        { code: row.code },
        { acceptLabelKey: 'pages.membership_plans.deactivate_confirm.yes' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deactivatePlanConfirmed(id));
  }

  private deactivatePlanConfirmed(id: string): void {
    this.deactivating.set(true);
    this.membershipClient
      .deactivate(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.deactivating.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated('pages.membership_plans.messages.deactivate_success');
          this.loadPlans();
        }
      });
  }
}
