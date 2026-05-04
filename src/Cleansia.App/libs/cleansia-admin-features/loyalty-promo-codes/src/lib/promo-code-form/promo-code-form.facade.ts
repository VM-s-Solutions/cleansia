import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreatePromoCodeCommand,
  CreatePromoCodeResponse,
  CurrencyListItem,
  PromoCodeDetailDto,
  PromoCodeType,
  UpdatePromoCodeCommand,
  UpdatePromoCodeResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface CurrencyOption {
  id: string;
  code: string;
  symbol: string | undefined;
}

/**
 * Backend stores discountPercent as a fraction (0..1). The form binds 0..100 for UX.
 * Conversion happens here, not in the component.
 */
export interface PromoCodeCreateInput {
  code: string;
  type: PromoCodeType;
  discountPercentUi?: number; // 0..100 (UI representation)
  discountAmount?: number;
  currencyId?: string;
  minimumOrderAmount?: number;
  maxRedemptionsPerUser: number;
  globalMaxRedemptions?: number;
  validFrom?: Date;
  validUntil?: Date;
  description?: string;
}

export interface PromoCodeUpdateInput {
  isActive: boolean;
  validFrom?: Date;
  validUntil?: Date;
  minimumOrderAmount?: number;
  maxRedemptionsPerUser: number;
  globalMaxRedemptions?: number;
  description?: string;
}

@Injectable()
export class PromoCodeFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly promoCode = signal<PromoCodeDetailDto | null>(null);
  readonly currencies = signal<CurrencyOption[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadPromoCode(id: string): void {
    this.loading.set(true);
    this.adminClient.adminPromoCodeClient
      .details(id)
      .pipe(
        takeUntil(this.destroy$),
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

  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of([] as CurrencyListItem[]))
      )
      .subscribe((items) => {
        this.currencies.set(
          items
            .filter(
              (c): c is CurrencyListItem & { id: string; code: string } =>
                Boolean(c.id) && Boolean(c.code)
            )
            .map((c) => ({
              id: c.id!,
              code: c.code!,
              symbol: c.symbol,
            }))
        );
      });
  }

  create(input: PromoCodeCreateInput): void {
    this.saving.set(true);

    const isPercent = input.type === PromoCodeType.PercentDiscount;
    const discountPercent =
      isPercent && input.discountPercentUi != null
        ? input.discountPercentUi / 100
        : undefined;

    const command = new CreatePromoCodeCommand({
      code: input.code,
      type: input.type,
      discountPercent: discountPercent,
      discountAmount: !isPercent ? input.discountAmount : undefined,
      currencyId: !isPercent ? input.currencyId : undefined,
      minimumOrderAmount: input.minimumOrderAmount,
      maxRedemptionsPerUser: input.maxRedemptionsPerUser,
      globalMaxRedemptions: input.globalMaxRedemptions,
      validFrom: input.validFrom,
      validUntil: input.validUntil,
      description: input.description,
    });

    this.adminClient.adminPromoCodeClient
      .create(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((err) => {
          this.handleSaveError(err);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: CreatePromoCodeResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.promo_codes.form.success.created')
          );
          if (response.promoCodeId) {
            this.router.navigate(['/loyalty/promos', response.promoCodeId]);
          } else {
            this.router.navigate(['/loyalty/promos']);
          }
        }
      });
  }

  update(id: string, input: PromoCodeUpdateInput): void {
    this.saving.set(true);

    const command = new UpdatePromoCodeCommand({
      promoCodeId: id,
      isActive: input.isActive,
      validFrom: input.validFrom,
      validUntil: input.validUntil,
      minimumOrderAmount: input.minimumOrderAmount,
      maxRedemptionsPerUser: input.maxRedemptionsPerUser,
      globalMaxRedemptions: input.globalMaxRedemptions,
      description: input.description,
    });

    this.adminClient.adminPromoCodeClient
      .update(id, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((err) => {
          this.handleSaveError(err);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdatePromoCodeResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.promo_codes.form.success.updated')
          );
          this.router.navigate(['/loyalty/promos', id]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/loyalty/promos']);
  }

  private handleSaveError(err: unknown): void {
    const detail =
      typeof err === 'object' && err && 'response' in err
        ? String((err as { response: unknown }).response ?? '')
        : '';
    if (detail.includes('promo_code.already_exists')) {
      this.snackbarService.showError(
        this.translate.instant('pages.promo_codes.form.error.already_exists')
      );
    } else {
      this.snackbarService.showError(
        this.translate.instant('pages.promo_codes.form.error.generic')
      );
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
