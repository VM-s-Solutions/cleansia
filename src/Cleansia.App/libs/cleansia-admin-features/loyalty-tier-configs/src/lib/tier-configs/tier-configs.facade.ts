import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  LoyaltyTier,
  PreviewTierThresholdImpactQuery,
  PreviewTierThresholdImpactResponse,
  PreviewTierThresholdImpactTierImpact,
  TierConfigAdminDto,
  UpdateTierConfigCommand,
  UpdateTierConfigResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import {
  catchError,
  concatMap,
  finalize,
  from,
  of,
  takeUntil,
  toArray,
} from 'rxjs';

/**
 * Backend stores discountPercent as a fraction (0..1). The form binds 0..100 for UX.
 * Matches the L4-A1 promo-code conversion convention.
 */
export interface TierConfigUpdateInput {
  lifetimePointsThreshold: number;
  discountPercentUi: number; // 0..100 (UI representation)
  minimumOrderAmountForDiscount?: number;
  perksJson?: string;
}

@Injectable()
export class TierConfigsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly tiers = signal<TierConfigAdminDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  readonly saving = signal<boolean>(false);

  readonly previewing = signal<boolean>(false);
  readonly applying = signal<boolean>(false);
  readonly previewResult = signal<PreviewTierThresholdImpactTierImpact[] | null>(
    null
  );

  loadTiers(): void {
    this.loading.set(true);
    this.adminClient.adminLoyaltyTierClient
      .getAll()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.tiers.set(response.tiers ?? []);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  update(id: string, input: TierConfigUpdateInput, onSuccess?: () => void): void {
    this.saving.set(true);

    const command = new UpdateTierConfigCommand({
      tierConfigId: id,
      lifetimePointsThreshold: input.lifetimePointsThreshold,
      discountPercent: input.discountPercentUi / 100,
      minimumOrderAmountForDiscount: input.minimumOrderAmountForDiscount,
      perksJson: input.perksJson,
    });

    this.adminClient.adminLoyaltyTierClient
      .update(id, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((err) => {
          this.handleSaveError(err);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdateTierConfigResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.loyalty_tiers.form.success')
          );
          this.loadTiers();
          onSuccess?.();
        }
      });
  }

  previewThresholds(
    bronzeThreshold: number,
    silverThreshold: number,
    goldThreshold: number,
    platinumThreshold: number
  ): void {
    this.previewing.set(true);

    const query = new PreviewTierThresholdImpactQuery({
      bronzeThreshold,
      silverThreshold,
      goldThreshold,
      platinumThreshold,
    });

    this.adminClient.adminLoyaltyTierClient
      .previewThresholdImpact(query)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.loyalty_tiers.preview.error.preview')
          );
          return of(null);
        }),
        finalize(() => this.previewing.set(false))
      )
      .subscribe((response: PreviewTierThresholdImpactResponse | null) => {
        if (response) {
          this.previewResult.set(response.impacts ?? []);
        }
      });
  }

  clearPreviewResult(): void {
    this.previewResult.set(null);
  }

  /**
   * Apply threshold updates for all tiers whose threshold actually changed.
   * The backend has no bulk endpoint — issue sequential Update calls and roll
   * up success/error reporting at the end.
   */
  applyThresholdChanges(
    proposedThresholds: Record<LoyaltyTier, number>,
    onAllSuccess?: () => void
  ): void {
    const current = this.tiers();
    const changed = current.filter(
      (t) =>
        t.id != null &&
        proposedThresholds[t.tier] !== undefined &&
        proposedThresholds[t.tier] !== t.lifetimePointsThreshold
    );

    if (changed.length === 0) {
      onAllSuccess?.();
      return;
    }

    this.applying.set(true);

    // Spec: sequential Update calls, not bulk. Use concatMap so each call
    // waits for the previous one to complete before firing.
    from(changed)
      .pipe(
        concatMap((t) => {
          const newThreshold = proposedThresholds[t.tier];
          const command = new UpdateTierConfigCommand({
            tierConfigId: t.id,
            lifetimePointsThreshold: newThreshold,
            discountPercent: t.discountPercent,
            minimumOrderAmountForDiscount: t.minimumOrderAmountForDiscount,
            perksJson: t.perksJson,
          });
          return this.adminClient.adminLoyaltyTierClient
            .update(t.id!, command)
            .pipe(catchError(() => of(null)));
        }),
        toArray(),
        takeUntil(this.destroyed$),
        finalize(() => this.applying.set(false))
      )
      .subscribe((results) => {
        const failed = results.some((r) => r == null);
        if (failed) {
          this.snackbarService.showError(
            this.translate.instant('pages.loyalty_tiers.preview.error.apply')
          );
          // Still reload to surface whatever did succeed.
          this.loadTiers();
          return;
        }
        this.snackbarService.showSuccess(
          this.translate.instant('pages.loyalty_tiers.preview.success')
        );
        this.loadTiers();
        onAllSuccess?.();
      });
  }

  private handleSaveError(_err: unknown): void {
    this.snackbarService.showError(
      this.translate.instant('pages.loyalty_tiers.form.error.generic')
    );
  }
}
