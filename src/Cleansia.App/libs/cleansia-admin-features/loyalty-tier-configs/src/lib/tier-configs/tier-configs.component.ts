import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  LoyaltyTier,
  TierConfigAdminDto,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { getTierConfigsTableDefinition, TierRow } from './tier-configs.models';
import { TierConfigsFacade } from './tier-configs.facade';

@Component({
  selector: 'cleansia-admin-tier-configs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTextareaComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './tier-configs.component.html',
  providers: [TierConfigsFacade],
})
export class TierConfigsComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(TierConfigsFacade);
  private readonly destroyRef = inject(DestroyRef);

  // Edit modal state
  readonly editDialogVisible = signal<boolean>(false);
  readonly editingTier = signal<TierRow | null>(null);
  readonly perksJsonValid = signal<boolean>(true);

  // Preview modal state
  readonly previewDialogVisible = signal<boolean>(false);

  // Stable display order for the preview / table.
  private readonly tierOrder: LoyaltyTier[] = [
    LoyaltyTier.BronzeCleaner,
    LoyaltyTier.SilverMopper,
    LoyaltyTier.GoldPolisher,
    LoyaltyTier.PlatinumSparkler,
  ];

  /**
   * Map raw DTOs into ordered rows with derived display fields.
   * Backend may return tiers in any order; we sort by the enum order so
   * Bronze always comes first.
   */
  readonly tierRows = computed<TierRow[]>(() => {
    const rows: TierRow[] = this.facade
      .tiers()
      .filter((t): t is TierConfigAdminDto & { id: string } => Boolean(t.id))
      .map((t) => {
        const perksCount = this.countPerks(t.perksJson);
        return {
          id: t.id,
          tier: t.tier,
          tierName: this.translate.instant(this.tierKey(t.tier)),
          threshold: t.lifetimePointsThreshold,
          discountPercent: t.discountPercent,
          discountFormatted: this.formatDiscount(t.discountPercent),
          minimumOrderAmountForDiscount: t.minimumOrderAmountForDiscount,
          minOrderFormatted: this.formatMinOrder(t.minimumOrderAmountForDiscount),
          perksJson: t.perksJson,
          perksCount,
          perksCountFormatted: this.formatPerksCount(perksCount),
          raw: t,
        };
      });
    rows.sort(
      (a, b) => this.tierOrder.indexOf(a.tier) - this.tierOrder.indexOf(b.tier)
    );
    return rows;
  });

  /**
   * Column + action definitions for the cleansia-table. Built once at
   * construction — header strings are resolved synchronously from the
   * current language, matching the pattern used in admin-user-management.
   */
  readonly tableDefinition = getTierConfigsTableDefinition(
    { onEdit: (row) => this.openEdit(row) },
    this.translate
  );

  // ---- Edit form (per-tier modal) ----
  readonly editForm = this.fb.group({
    threshold: this.fb.control<number>(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    discountPercentUi: this.fb.control<number>(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0), Validators.max(100)],
    }),
    minimumOrderAmount: this.fb.control<number | null>(null, {
      validators: [Validators.min(0)],
    }),
    perksJson: this.fb.control<string>('', { nonNullable: true }),
  });

  // ---- Preview form (cross-tier thresholds) ----
  readonly previewForm = this.fb.group({
    bronzeThreshold: this.fb.control<number>(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    silverThreshold: this.fb.control<number>(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    goldThreshold: this.fb.control<number>(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    platinumThreshold: this.fb.control<number>(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
  });

  ngOnInit(): void {
    this.facade.loadTiers();

    // Live perks JSON validation: yellow flag, doesn't block submit.
    this.editForm.controls.perksJson.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        this.perksJsonValid.set(this.isValidPerksJson(value));
      });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  // ---- Display helpers ----

  tierKey(tier: LoyaltyTier): string {
    switch (tier) {
      case LoyaltyTier.BronzeCleaner:
        return 'pages.loyalty_tiers.tier.BronzeCleaner';
      case LoyaltyTier.SilverMopper:
        return 'pages.loyalty_tiers.tier.SilverMopper';
      case LoyaltyTier.GoldPolisher:
        return 'pages.loyalty_tiers.tier.GoldPolisher';
      case LoyaltyTier.PlatinumSparkler:
        return 'pages.loyalty_tiers.tier.PlatinumSparkler';
      default:
        return '';
    }
  }

  formatDiscount(fraction: number): string {
    return this.translate.instant('pages.loyalty_tiers.discount_format', {
      percent: Math.round(fraction * 100),
    });
  }

  formatMinOrder(amount?: number): string {
    if (amount == null) {
      return this.translate.instant('pages.loyalty_tiers.no_min_order');
    }
    return `${amount}`;
  }

  formatPerksCount(count: number): string {
    return this.translate.instant('pages.loyalty_tiers.perks_count', { count });
  }

  // ---- Edit modal ----

  openEdit(row: TierRow): void {
    this.editingTier.set(row);
    this.editForm.reset({
      threshold: row.threshold,
      // Convert backend fraction (0..1) -> UI percent (0..100).
      discountPercentUi: Math.round(row.discountPercent * 100),
      minimumOrderAmount: row.minimumOrderAmountForDiscount ?? null,
      perksJson: row.perksJson ?? '',
    });
    this.perksJsonValid.set(this.isValidPerksJson(row.perksJson ?? ''));
    this.editDialogVisible.set(true);
  }

  closeEdit(): void {
    this.editDialogVisible.set(false);
    this.editingTier.set(null);
  }

  onEditSubmit(): void {
    const tier = this.editingTier();
    if (!tier) return;
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }
    const v = this.editForm.getRawValue();
    this.facade.update(
      tier.id,
      {
        lifetimePointsThreshold: v.threshold,
        discountPercentUi: v.discountPercentUi,
        minimumOrderAmountForDiscount: v.minimumOrderAmount ?? undefined,
        perksJson: v.perksJson?.trim() ? v.perksJson : undefined,
      },
      () => this.closeEdit()
    );
  }

  // ---- Preview modal ----

  openPreview(): void {
    const rows = this.tierRows();
    const byTier = (t: LoyaltyTier) =>
      rows.find((r) => r.tier === t)?.threshold ?? 0;
    this.previewForm.reset({
      bronzeThreshold: byTier(LoyaltyTier.BronzeCleaner),
      silverThreshold: byTier(LoyaltyTier.SilverMopper),
      goldThreshold: byTier(LoyaltyTier.GoldPolisher),
      platinumThreshold: byTier(LoyaltyTier.PlatinumSparkler),
    });
    this.facade.clearPreviewResult();
    this.previewDialogVisible.set(true);
  }

  closePreview(): void {
    this.previewDialogVisible.set(false);
    this.facade.clearPreviewResult();
  }

  onPreviewSubmit(): void {
    if (this.previewForm.invalid) {
      this.previewForm.markAllAsTouched();
      return;
    }
    const v = this.previewForm.getRawValue();
    this.facade.previewThresholds(
      v.bronzeThreshold,
      v.silverThreshold,
      v.goldThreshold,
      v.platinumThreshold
    );
  }

  onApplyChanges(): void {
    if (this.previewForm.invalid) {
      this.previewForm.markAllAsTouched();
      return;
    }
    const v = this.previewForm.getRawValue();
    const proposed = {
      [LoyaltyTier.BronzeCleaner]: v.bronzeThreshold,
      [LoyaltyTier.SilverMopper]: v.silverThreshold,
      [LoyaltyTier.GoldPolisher]: v.goldThreshold,
      [LoyaltyTier.PlatinumSparkler]: v.platinumThreshold,
    } as Record<LoyaltyTier, number>;
    this.facade.applyThresholdChanges(proposed, () => this.closePreview());
  }

  formatDelta(delta: number): string {
    return delta >= 0 ? `+${delta}` : `${delta}`;
  }

  // ---- Perks JSON helpers ----

  /** Empty is treated as valid (= no perks). */
  private isValidPerksJson(raw: string | null | undefined): boolean {
    if (!raw || !raw.trim()) return true;
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed);
    } catch {
      return false;
    }
  }

  private countPerks(raw: string | null | undefined): number {
    if (!raw || !raw.trim()) return 0;
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.length : 0;
    } catch {
      return 0;
    }
  }
}
