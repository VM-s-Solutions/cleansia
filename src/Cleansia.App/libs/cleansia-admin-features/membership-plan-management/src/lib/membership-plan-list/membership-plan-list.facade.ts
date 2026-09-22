import { Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminMembershipClient,
  MembershipPlanListItem,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { DialogService, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
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
  private readonly translate = inject(TranslateService);

  readonly plans = signal<MembershipPlanListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);
  readonly deactivating = signal<boolean>(false);

  readonly lang = currentLanguage(this.translate);
  readonly filterForm = inject(FormBuilder).nonNullable.group({
    search: [''],
    activeOnly: [false],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.search.trim()
        ? [
            {
              key: 'search',
              label: this.translate.instant('pages.membership_plans.filter.search_label'),
              value: value.search.trim(),
            },
          ]
        : []),
      ...(value.activeOnly
        ? [
            {
              key: 'activeOnly',
              label: this.translate.instant('pages.membership_plans.filter.status'),
              value: this.translate.instant('pages.membership_plans.filter.active_only'),
            },
          ]
        : []),
    ],
    apply: (value) =>
      this.applyFilter({
        search: value.search.trim() || undefined,
        active: value.activeOnly ? true : undefined,
      }),
  });

  private readonly currentFilter = signal<MembershipPlanFilterParams | null>(
    null
  );
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

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
