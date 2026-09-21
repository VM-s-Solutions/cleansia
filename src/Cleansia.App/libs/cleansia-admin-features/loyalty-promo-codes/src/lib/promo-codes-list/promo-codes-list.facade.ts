import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminClient,
  PromoCodeListItem,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { DialogService, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

export type PromoCodeStatusFilter = 'all' | 'active' | 'inactive' | 'expired';

export interface PromoCodeFilterParams {
  searchCode?: string;
  status?: PromoCodeStatusFilter;
}

const STATUS_FILTER_LABEL_KEYS: Readonly<Record<PromoCodeStatusFilter, string>> = {
  all: 'pages.promo_codes.status_filter_all',
  active: 'pages.promo_codes.status_filter_active',
  inactive: 'pages.promo_codes.status_filter_inactive',
  expired: 'pages.promo_codes.status_filter_expired',
};

@Injectable()
export class PromoCodesListFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly promoCodes = signal<PromoCodeListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
  readonly statusFilterOptions = computed<ICleansiaSelectOption[]>(() => {
    this.lang();
    return (Object.keys(STATUS_FILTER_LABEL_KEYS) as PromoCodeStatusFilter[]).map((value) => ({
      label: this.translate.instant(STATUS_FILTER_LABEL_KEYS[value]),
      value,
    }));
  });
  readonly filterForm = inject(FormBuilder).nonNullable.group({
    searchCode: [''],
    status: ['all' as PromoCodeStatusFilter],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.searchCode.trim()
        ? [
            {
              key: 'searchCode',
              label: this.translate.instant('pages.promo_codes.filters.search'),
              value: value.searchCode.trim(),
            },
          ]
        : []),
      ...(value.status !== 'all'
        ? [
            {
              key: 'status',
              label: this.translate.instant('pages.promo_codes.filters.status'),
              value: this.translate.instant(STATUS_FILTER_LABEL_KEYS[value.status]),
            },
          ]
        : []),
    ],
    apply: (value) =>
      this.applyFilter({
        searchCode: value.searchCode.trim() || undefined,
        status: value.status,
      }),
  });

  private currentFilter = signal<PromoCodeFilterParams>({ status: 'all' });
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadPromoCodes(): void {
    this.loading.set(true);
    const filter = this.currentFilter();
    const { active, expired } = this.toServerFlags(filter.status);

    this.adminClient.adminPromoCodeClient
      .getPaged(
        active,
        expired,
        filter.searchCode,
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
          this.promoCodes.set(response.data ?? []);
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
    this.loadPromoCodes();
  }

  applyFilter(filter: PromoCodeFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPromoCodes();
  }

  deactivate(promoCode: PromoCodeListItem): void {
    const id = promoCode.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.promo_codes.detail.deactivate_confirm_body',
        'pages.promo_codes.detail.deactivate_confirm_title',
        undefined,
        { acceptLabelKey: 'pages.promo_codes.detail.deactivate_confirm_yes' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deactivateConfirmed(id));
  }

  private deactivateConfirmed(id: string): void {
    this.loading.set(true);

    this.adminClient.adminPromoCodeClient
      .deactivate(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error) => {
          this.snackbarService.showApiError(error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated('pages.promo_codes.form.success.deactivated');
          this.loadPromoCodes();
        }
      });
  }

  navigateToCreate(): void {
    this.router.navigate(['/loyalty/promos', 'new']);
  }

  navigateToDetail(promoCode: PromoCodeListItem): void {
    if (promoCode.id) {
      this.router.navigate(['/loyalty/promos', promoCode.id]);
    }
  }

  navigateToEdit(promoCode: PromoCodeListItem): void {
    if (promoCode.id) {
      this.router.navigate(['/loyalty/promos', promoCode.id, 'edit']);
    }
  }

  /**
   * Maps the UI status filter to backend (active, expired) booleans.
   * Backend filters are independent: active=true means IsActive, expired=true means past ValidUntil.
   * - all       => both undefined (no filter)
   * - active    => active=true
   * - inactive  => active=false
   * - expired   => expired=true
   */
  private toServerFlags(status: PromoCodeStatusFilter | undefined): {
    active: boolean | undefined;
    expired: boolean | undefined;
  } {
    switch (status) {
      case 'active':
        return { active: true, expired: undefined };
      case 'inactive':
        return { active: false, expired: undefined };
      case 'expired':
        return { active: undefined, expired: true };
      case 'all':
      default:
        return { active: undefined, expired: undefined };
    }
  }
}
