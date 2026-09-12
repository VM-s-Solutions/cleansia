import { Injectable, inject, signal } from '@angular/core';
import {
  AdminCurrencyClient,
  AdminCurrencyListItem,
  AdminMembershipClient,
  MembershipPlanListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, map, of, switchMap, takeUntil, tap } from 'rxjs';
import { resolveMembershipPlanErrorKey } from './membership-plan-list.models';

export interface MembershipPlanFilterParams {
  active?: boolean;
  search?: string;
}

@Injectable()
export class MembershipPlanListFacade extends UnsubscribeControlDirective {
  private readonly membershipClient = inject(AdminMembershipClient);
  private readonly currencyClient = inject(AdminCurrencyClient);
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

  /**
   * The currency the plan prices are in: the platform default, read once from the currency overview.
   * The DTO's fields are named *Czk but carry no code of their own, and the label must follow the
   * platform, not the field name. Null until known; the formatter then prints the bare number.
   */
  readonly defaultCurrencyCode = signal<string | null>(null);

  loadPlans(): void {
    this.loading.set(true);
    this.hasError.set(false);
    const filter = this.currentFilter();

    // The currency first, once, so the rows never render under a label that arrives later.
    const currency$: Observable<string | null> =
      this.defaultCurrencyCode() !== null
        ? of(this.defaultCurrencyCode())
        : this.currencyClient.getOverview().pipe(
            catchError(() => of([] as AdminCurrencyListItem[])),
            map((currencies) => (currencies ?? []).find((c) => c.isDefault)?.code ?? null),
            tap((code) => this.defaultCurrencyCode.set(code))
          );

    currency$
      .pipe(
        switchMap(() =>
          this.membershipClient.getPaged(
            filter?.active,
            filter?.search,
            this.currentOffset(),
            this.currentLimit()
          )
        ),
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

  formatPrice(value: number | undefined | null): string {
    if (value == null) return '—';
    return `${value.toFixed(2)} ${this.defaultCurrencyCode() ?? ''}`.trimEnd();
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
