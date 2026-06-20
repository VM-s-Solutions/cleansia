import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  PromoCodeListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export type PromoCodeStatusFilter = 'all' | 'active' | 'inactive' | 'expired';

export interface PromoCodeFilterParams {
  searchCode?: string;
  status?: PromoCodeStatusFilter;
}

@Injectable()
export class PromoCodesListFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly promoCodes = signal<PromoCodeListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<PromoCodeFilterParams>({ status: 'all' });
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);

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

  resetFilter(): void {
    this.currentFilter.set({ status: 'all' });
    this.currentOffset.set(0);
    this.loadPromoCodes();
  }

  deactivate(promoCode: PromoCodeListItem): void {
    if (!promoCode.id) return;

    this.loading.set(true);

    this.adminClient.adminPromoCodeClient
      .deactivate(promoCode.id)
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
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.promo_codes.form.success.deactivated'
            )
          );
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
