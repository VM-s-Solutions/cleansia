import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, Input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerClient, PaymentType } from '@cleansia/customer-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, of } from 'rxjs';
import { InputTextModule } from 'primeng/inputtext';
import {
  CleansiaCodeInputDialogComponent,
  CodeDialogResult,
} from '@cleansia/components';
import { OrderWizardFacade } from '../order-wizard.facade';
import { formatPrice, getItemTranslation, PromoCodeUiState } from '../order-wizard.models';

/**
 * Map the backend's PromoCodeError enum (string) to a localized i18n key.
 * Unknown / null error codes fall back to a generic "couldn't validate" key.
 */
const PROMO_ERROR_KEYS: Record<string, string> = {
  NotFound: 'pages.order.promo.error_not_found',
  Inactive: 'pages.order.promo.error_inactive',
  Expired: 'pages.order.promo.error_expired',
  NotYetValid: 'pages.order.promo.error_not_yet_valid',
  GlobalLimitReached: 'pages.order.promo.error_global_limit',
  PerUserLimitReached: 'pages.order.promo.error_used',
  BelowMinimumOrderAmount: 'pages.order.promo.error_min_order',
  CurrencyMismatch: 'pages.order.promo.error_currency',
};

@Component({
  selector: 'cleansia-wizard-summary-step',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, InputTextModule, TranslatePipe, CleansiaCodeInputDialogComponent],
  templateUrl: './wizard-summary-step.component.html',
})
export class WizardSummaryStepComponent implements OnInit {
  @Input({ required: true }) facade!: OrderWizardFacade;
  private readonly translate = inject(TranslateService);
  // Always go through CustomerClient — direct injection of MembershipClient
  // hits NSwag's empty-string default baseUrl and bypasses CUSTOMER_API_BASE_URL.
  private readonly customerClient = inject(CustomerClient);
  protected readonly PaymentType = PaymentType;
  protected readonly formatPriceFn = formatPrice;

  /**
   * Hours in the user's free-cancellation window when they hold an active
   * Plus membership. Null when not Plus or not yet loaded — template falls
   * back to the standard 24h tier-1 copy. One-shot fetch on init; the
   * cancellation card is render-once and doesn't need live updates.
   */
  protected readonly plusFreeHours = signal<number | null>(null);

  ngOnInit(): void {
    // Anonymous users can't hold a Plus membership — skip the call to avoid a
    // noisy 401 on the booking wizard before sign-in.
    if (!this.facade.isAuthenticated()) return;
    this.customerClient.membershipClient
      .getMine()
      .pipe(catchError(() => of(null)))
      .subscribe((response) => {
        if (response?.hasMembership && response.freeCancellationWindowHours) {
          this.plusFreeHours.set(response.freeCancellationWindowHours);
        }
      });
  }

  // ─── Dialog visibility ──────────────────────────────────────
  //
  // Local-only state; the facade owns the actual code + validation result.
  // The dialogs read facade signals via `*DialogState` computeds below.
  protected readonly promoDialogVisible = signal(false);

  openPromoDialog(): void {
    this.promoDialogVisible.set(true);
  }

  closePromoDialog(): void {
    this.promoDialogVisible.set(false);
  }

  /** Bridge dialog `(visibleChange)` into the local signal — types align cleanly. */
  onPromoDialogVisible(visible: boolean): void {
    this.promoDialogVisible.set(visible);
  }

  /**
   * Row clear-X handler. stopPropagation so the click doesn't bubble up to
   * the row's `(click)="openPromoDialog()"` and reopen the dialog we just
   * cleared from.
   */
  clearPromo(event: Event): void {
    event.stopPropagation();
    this.facade.clearPromoCode();
  }

  /** Apply tap handler — single backend call, no debounce. */
  async applyPromo(code: string): Promise<void> {
    await this.facade.validatePromoCodeNow(code);
  }

  /**
   * Adapt the facade's promo state into the dialog's generic shape, baking in
   * the localized success / error messages. Computed so it reacts to both the
   * facade signal and the active translate language.
   */
  protected readonly promoDialogState = computed<CodeDialogResult>(() => {
    const state = this.facade.promoCodeState();
    return promoStateToDialog(state, this.translate);
  });

  /** Normalized display form for the summary "Promo (-CODE)" line. */
  protected readonly promoCodeDisplay = computed(() =>
    this.facade.promoCode().trim().toUpperCase(),
  );

  /** Pretty CZK string for the discount amount, used in subtitle + summary row. */
  protected readonly formattedPromoDiscount = computed(() => {
    const state = this.facade.promoCodeState();
    return formatPrice(state.kind === 'valid' ? state.discount : 0);
  });

  /**
   * Final price — the server-quoted total (express surcharge already folded
   * in) minus the best-of-three discount. Wrapped in computed because `facade`
   * is an @Input and isn't bound at field-init time.
   */
  protected readonly grandTotal = computed(() => this.facade.displayedTotalPrice());

  /**
   * Render the membership chip when the membership discount wins (or ties promo).
   * Mutually exclusive with the tier chip — only one renders.
   */
  // LOY-003 — Plus and tier are additive on the wire (server already
  // returns both, capped at 12% combined). Promo replaces the combined
  // pair when larger. So we show the membership chip whenever Plus has a
  // non-zero amount AND promo isn't winning over the combined; same logic
  // for the tier chip. Both can appear simultaneously.
  protected readonly showMembershipChip = computed(() => {
    const m = this.facade.membershipDiscount();
    if (m <= 0) return false;
    const combined = m + this.facade.tierDiscount();
    return this.facade.effectivePromoDiscount() <= combined;
  });

  protected readonly showTierChip = computed(() => {
    const t = this.facade.tierDiscount();
    if (t <= 0) return false;
    const combined = t + this.facade.membershipDiscount();
    return this.facade.effectivePromoDiscount() <= combined;
  });

  /** "Loyalty discount needs orders above X" hint when the floor wasn't met. */
  protected readonly showTierFloorHint = computed(() => {
    const floor = this.facade.tierDiscountMinOrderAmount();
    if (floor == null || floor <= 0) return false;
    if (this.facade.effectiveDiscount() > 0) return false;
    return this.facade.totalPrice() < floor;
  });

  protected readonly tierFloorAmount = computed(() => {
    const floor = this.facade.tierDiscountMinOrderAmount();
    return floor != null ? formatPrice(floor) : '';
  });

  protected readonly membershipDiscountFormatted = computed(() =>
    formatPrice(this.facade.membershipDiscount()),
  );

  protected readonly tierDiscountFormatted = computed(() =>
    formatPrice(this.facade.tierDiscount()),
  );

  readonly selectedServices = computed(() => {
    const ids = this.facade.formData().selectedServiceIds;
    return this.facade.services().filter((s) => s.id && ids.includes(s.id));
  });

  readonly selectedPackages = computed(() => {
    const ids = this.facade.formData().selectedPackageIds;
    return this.facade.packages().filter((p) => p.id && ids.includes(p.id));
  });

  readonly serviceRows = computed(() =>
    this.selectedServices().map((s) => ({
      label: getItemTranslation(s, 'name', this.translate),
      value: formatPrice(s.basePrice),
    }))
  );

  readonly packageRows = computed(() =>
    this.selectedPackages().map((p) => ({
      label: getItemTranslation(p, 'name', this.translate),
      value: formatPrice(p.price),
    }))
  );

  readonly propertyRows = computed(() => {
    const d = this.facade.formData();
    return [
      { label: this.translate.instant('pages.order.rooms_label'), value: String(d.rooms) },
      { label: this.translate.instant('pages.order.bathrooms_label'), value: String(d.bathrooms) },
    ];
  });

  readonly addressRows = computed(() => {
    const a = this.facade.formData().address;
    return [{ label: `${a.street}, ${a.city} ${a.zipCode}` }];
  });

  readonly dateTimeRows = computed(() => {
    const d = this.facade.formData();
    const dateStr = d.cleaningDate
      ? new Date(d.cleaningDate).toLocaleDateString('cs-CZ')
      : '';
    return [{ label: `${dateStr} ${d.cleaningTime}` }];
  });

  readonly paymentRows = computed(() => {
    const key =
      this.facade.formData().paymentType === PaymentType.Card
        ? 'pages.order.payment_card_title'
        : 'pages.order.payment_cash_title';
    return [{ label: this.translate.instant(key) }];
  });

  readonly contactRows = computed(() => {
    const d = this.facade.formData();
    return [
      { label: `${d.customerFirstName} ${d.customerLastName}` },
      { label: d.customerEmail },
      { label: d.customerPhone },
    ];
  });

  readonly instructionRows = computed(() => {
    const d = this.facade.formData();
    const rows: { label: string }[] = [];
    if (d.specialInstructions) rows.push({ label: d.specialInstructions });
    if (d.entryInstructions) rows.push({ label: d.entryInstructions });
    return rows;
  });

  /**
   * Unicode emoji glyph for a known extra slug — used as the medallion
   * icon. We use emoji rather than PrimeIcons because PrimeIcons 7 lacks
   * kitchen / cleaning glyphs (no oven, fridge, iron, etc.). Falls back to
   * a generic ✨ so future-seeded extras still render without a code
   * change. Map keys mirror `insert_seed_data.sql` (7b. EXTRAS).
   */
  protected iconForExtraSlug(slug: string | null | undefined): string {
    switch (slug) {
      case 'inside-oven':
        return '🔥';
      case 'inside-fridge':
        return '❄️';
      case 'interior-windows':
        return '🪟';
      case 'laundry-ironing':
        return '🧺';
      case 'pet-hair-supplement':
        return '🐾';
      default:
        return '✨';
    }
  }
}

/** Format the localized discount string for the success message. */
function promoStateToDialog(
  state: PromoCodeUiState,
  translate: TranslateService,
): CodeDialogResult {
  switch (state.kind) {
    case 'idle':
      return { kind: 'idle' };
    case 'validating':
      return { kind: 'validating' };
    case 'valid':
      return {
        kind: 'valid',
        successMessage: translate.instant('pages.order.promo.dialog_success', {
          amount: formatPrice(state.discount),
        }),
      };
    case 'invalid': {
      const key =
        PROMO_ERROR_KEYS[state.error ?? ''] ?? 'pages.order.promo.error_generic';
      return { kind: 'invalid', errorMessage: translate.instant(key) };
    }
  }
}
