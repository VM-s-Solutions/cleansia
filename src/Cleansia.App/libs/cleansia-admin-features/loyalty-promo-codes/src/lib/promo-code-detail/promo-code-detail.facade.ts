import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  PromoCodeDetailDto,
  PromoCodeRedemptionListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class PromoCodeDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly promoCode = signal<PromoCodeDetailDto | null>(null);
  readonly loading = signal<boolean>(false);

  readonly redemptions = signal<PromoCodeRedemptionListItem[]>([]);
  readonly redemptionsLoading = signal<boolean>(false);
  readonly redemptionsTotal = signal<number>(0);

  private currentRedemptionsOffset = 0;
  private currentRedemptionsLimit = 20;
  private currentId: string | null = null;

  loadPromoCode(id: string): void {
    this.currentId = id;
    this.loading.set(true);
    this.adminClient.adminPromoCodeClient
      .details(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.promoCode.set(response);
        } else {
          this.router.navigate(['/loyalty/promos']);
        }
      });
  }

  loadRedemptions(id: string, offset = 0, limit = 20): void {
    this.currentId = id;
    this.currentRedemptionsOffset = offset;
    this.currentRedemptionsLimit = limit;
    this.redemptionsLoading.set(true);
    this.adminClient.adminPromoCodeClient
      .getRedemptions(id, offset, limit)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.redemptionsLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.redemptions.set(response.data ?? []);
          this.redemptionsTotal.set(response.total ?? 0);
        }
      });
  }

  onRedemptionsPageChange(offset: number, limit: number): void {
    if (!this.currentId) return;
    this.loadRedemptions(this.currentId, offset, limit);
  }

  deactivate(): void {
    if (!this.currentId) return;
    this.adminClient.adminPromoCodeClient
      .deactivate(this.currentId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.promo_codes.form.success.deactivated'
            )
          );
          if (this.currentId) {
            this.loadPromoCode(this.currentId);
          }
        }
      });
  }

  navigateToEdit(): void {
    if (this.currentId) {
      this.router.navigate(['/loyalty/promos', this.currentId, 'edit']);
    }
  }

  navigateToList(): void {
    this.router.navigate(['/loyalty/promos']);
  }
}
