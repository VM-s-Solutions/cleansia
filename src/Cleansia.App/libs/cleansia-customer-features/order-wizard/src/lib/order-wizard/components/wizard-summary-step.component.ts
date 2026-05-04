import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, Input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerClient } from '@cleansia/customer-services';
import { PaymentType } from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, of } from 'rxjs';
import { InputTextModule } from 'primeng/inputtext';
import {
  CleansiaCodeInputDialogComponent,
  CodeDialogResult,
} from '@cleansia/components';
import { OrderWizardFacade } from '../order-wizard.facade';
import { formatPrice, getItemTranslation, PromoCodeUiState, ReferralUiState } from '../order-wizard.models';

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

/**
 * Map the backend's ReferralValidationError enum (string) to a localized i18n key.
 * Same fallback shape as the promo errorKey helper.
 */
const REFERRAL_ERROR_KEYS: Record<string, string> = {
  NotFound: 'pages.order.referral.error_not_found',
  SelfReferral: 'pages.order.referral.error_self_referral',
  AlreadyReferred: 'pages.order.referral.error_already_referred',
  Inactive: 'pages.order.referral.error_inactive',
};

@Component({
  selector: 'cleansia-wizard-summary-step',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, InputTextModule, TranslatePipe, CleansiaCodeInputDialogComponent],
  template: `
    <div class="order-wizard__step">
      <div class="order-wizard__section">
        <h2 class="order-wizard__section-title">
          <i class="pi pi-check-circle"></i>
          {{ 'pages.order.summary_section_title' | translate }}
        </h2>

        @if (selectedServices().length > 0) {
          <ng-container
            *ngTemplateOutlet="summaryCard; context: {
              icon: 'pi-list',
              titleKey: 'pages.order.summary_services',
              editStep: 0,
              rows: serviceRows()
            }"
          />
        }

        @if (selectedPackages().length > 0) {
          <ng-container
            *ngTemplateOutlet="summaryCard; context: {
              icon: 'pi-box',
              titleKey: 'pages.order.summary_packages',
              editStep: 0,
              rows: packageRows()
            }"
          />
        }

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-building',
            titleKey: 'pages.order.summary_property',
            editStep: 0,
            rows: propertyRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-map-marker',
            titleKey: 'pages.order.summary_address',
            editStep: 1,
            rows: addressRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-calendar',
            titleKey: 'pages.order.summary_datetime',
            editStep: 2,
            rows: dateTimeRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-credit-card',
            titleKey: 'pages.order.summary_payment',
            editStep: 3,
            rows: paymentRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-user',
            titleKey: 'pages.order.summary_contact',
            editStep: 1,
            rows: contactRows()
          }"
        />

        @if (facade.formData().specialInstructions || facade.formData().entryInstructions) {
          <ng-container
            *ngTemplateOutlet="summaryCard; context: {
              icon: 'pi-comment',
              titleKey: 'pages.order.summary_instructions',
              editStep: 3,
              rows: instructionRows()
            }"
          />
        }

        <!-- Referral row — Wolt-style entry tile that opens a dialog. Backend
             treats invalid codes as best-effort, so the row stays clickable
             after a failure to let the user retry. -->
        <div
          class="order-wizard__code-row"
          tabindex="0"
          role="button"
          (click)="openReferralDialog()"
          (keydown.enter)="openReferralDialog()"
        >
          <div class="order-wizard__code-row-icon">
            <i class="pi pi-users"></i>
          </div>
          <div class="order-wizard__code-row-content">
            <div class="order-wizard__code-row-label">
              {{ 'pages.order.referral.row_title' | translate }}
            </div>
            @if (facade.referralState().kind === 'valid') {
              <div class="order-wizard__code-row-applied">
                {{ 'pages.order.referral.row_applied' | translate: { code: facade.referralCode() } }}
              </div>
            }
          </div>
          <div class="order-wizard__code-row-action">
            @if (facade.referralState().kind === 'valid') {
              <button
                type="button"
                class="order-wizard__code-row-clear"
                [attr.aria-label]="'pages.order.referral.dialog_cancel' | translate"
                (click)="clearReferral($event)"
              >
                <i class="pi pi-times"></i>
              </button>
            } @else {
              <i class="pi pi-chevron-right"></i>
            }
          </div>
        </div>

        <!-- Promo row — Wolt-style. Tap to open dialog → enter code → Apply
             hits backend once → Done locks it in. The clear-X strips the
             applied state without reopening the dialog. -->
        <div
          class="order-wizard__code-row"
          tabindex="0"
          role="button"
          (click)="openPromoDialog()"
          (keydown.enter)="openPromoDialog()"
        >
          <div class="order-wizard__code-row-icon">
            <i class="pi pi-tag"></i>
          </div>
          <div class="order-wizard__code-row-content">
            <div class="order-wizard__code-row-label">
              {{ 'pages.order.promo.row_title' | translate }}
            </div>
            @if (facade.promoCodeState().kind === 'valid') {
              <div class="order-wizard__code-row-applied">
                {{ 'pages.order.promo.row_applied' | translate: { code: facade.promoCode() } }}
              </div>
            }
          </div>
          <div class="order-wizard__code-row-action">
            @if (facade.promoCodeState().kind === 'valid') {
              <button
                type="button"
                class="order-wizard__code-row-clear"
                [attr.aria-label]="'pages.order.promo.dialog_cancel' | translate"
                (click)="clearPromo($event)"
              >
                <i class="pi pi-times"></i>
              </button>
            } @else {
              <i class="pi pi-chevron-right"></i>
            }
          </div>
        </div>

        <!-- Totals — subtotal + promo discount line (only when valid) + grand total.
             Best-wins logic: when client-side tier discount preview ships later,
             render whichever discount is larger here; backend re-picks the larger
             of (tier, promo) at order-create time so the customer never overpays. -->
        <div class="order-wizard__summary-card">
          <div class="order-wizard__summary-card-header">
            <div class="order-wizard__summary-card-icon">
              <i class="pi pi-calculator"></i>
            </div>
            <h3>{{ 'pages.order.summary.totals_title' | translate }}</h3>
          </div>
          <div class="order-wizard__summary-card-body">
            <div class="order-wizard__summary-row">
              <span>{{ 'pages.order.summary.subtotal' | translate }}</span>
              <span class="order-wizard__summary-row-price">{{ formatPriceFn(facade.totalPrice()) }}</span>
            </div>
            @if (facade.promoCodeState().kind === 'valid') {
              <div class="order-wizard__summary-row order-wizard__summary-row--discount">
                <span>{{ 'pages.order.summary.promo_discount' | translate: { code: promoCodeDisplay() } }}</span>
                <span class="order-wizard__summary-row-price order-wizard__summary-row-price--discount">
                  -{{ formattedPromoDiscount() }}
                </span>
              </div>
            }
            <div class="order-wizard__summary-row order-wizard__summary-row--total">
              <span>{{ 'pages.order.price_total' | translate }}</span>
              <span class="order-wizard__summary-row-price">{{ formatPriceFn(grandTotal()) }}</span>
            </div>
          </div>
        </div>

        <!-- Cancellation policy — disclosure required by Czech Civil Code §1811 before payment.
             Styled as a summary-card sibling so it feels like part of the review, not an afterthought.
             Plus members see the wider free window from their plan. -->
        <div class="order-wizard__summary-card order-wizard__cancel-policy">
          <div class="order-wizard__summary-card-header">
            <div class="order-wizard__summary-card-icon">
              <i class="pi pi-clock"></i>
            </div>
            <h3>{{ 'pages.order.cancel_policy_title' | translate }}</h3>
            @if (plusFreeHours(); as plusH) {
              <span class="order-wizard__plus-badge">{{ 'pages.membership.plus_badge' | translate }}</span>
            }
          </div>
          <div class="order-wizard__summary-card-body">
            <div class="order-wizard__cancel-policy-row">
              <span>{{ 'pages.order.cancel_policy_tier1_when' | translate }}</span>
              <strong class="order-wizard__cancel-policy-free">{{ 'pages.order.cancel_policy_tier1_value' | translate }}</strong>
            </div>
            <div class="order-wizard__cancel-policy-row">
              @if (plusFreeHours(); as plusH) {
                <span>{{ 'pages.order.cancel_policy_tier2_when_plus' | translate: { hours: plusH } }}</span>
              } @else {
                <span>{{ 'pages.order.cancel_policy_tier2_when' | translate }}</span>
              }
              <strong class="order-wizard__cancel-policy-free">{{ 'pages.order.cancel_policy_tier2_value' | translate }}</strong>
            </div>
            <div class="order-wizard__cancel-policy-row">
              <span>{{ 'pages.order.cancel_policy_tier3_when' | translate }}</span>
              <strong class="order-wizard__cancel-policy-mid">{{ 'pages.order.cancel_policy_tier3_value' | translate }}</strong>
            </div>
            <div class="order-wizard__cancel-policy-row">
              <span>{{ 'pages.order.cancel_policy_tier4_when' | translate }}</span>
              <strong class="order-wizard__cancel-policy-full">{{ 'pages.order.cancel_policy_tier4_value' | translate }}</strong>
            </div>
          </div>
        </div>
      </div>
    </div>

    <ng-template #summaryCard let-icon="icon" let-titleKey="titleKey" let-editStep="editStep" let-rows="rows">
      <div class="order-wizard__summary-card">
        <div class="order-wizard__summary-card-header">
          <div class="order-wizard__summary-card-icon"><i class="pi {{ icon }}"></i></div>
          <h3>{{ titleKey | translate }}</h3>
          <button class="order-wizard__summary-edit" (click)="facade.goToStep(editStep)">
            {{ 'pages.order.summary_edit' | translate }}
          </button>
        </div>
        <div class="order-wizard__summary-card-body">
          @for (row of rows; track $index) {
            <div class="order-wizard__summary-row">
              <span>{{ row.label }}</span>
              @if (row.value) {
                <span class="order-wizard__summary-row-price">{{ row.value }}</span>
              }
            </div>
          }
        </div>
      </div>
    </ng-template>

    <!-- Code dialogs — rendered at the end so backdrop overlay layering Just Works. -->
    <cleansia-code-input-dialog
      [visible]="promoDialogVisible()"
      (visibleChange)="onPromoDialogVisible($event)"
      [initialCode]="facade.promoCode()"
      [state]="promoDialogState()"
      titleKey="pages.order.promo.dialog_title"
      inputLabelKey="pages.order.promo.row_title"
      helperKey="pages.order.promo.dialog_helper"
      (applyClicked)="applyPromo($event)"
      (done)="closePromoDialog()"
      (cancelled)="closePromoDialog()"
    />

    <cleansia-code-input-dialog
      [visible]="referralDialogVisible()"
      (visibleChange)="onReferralDialogVisible($event)"
      [initialCode]="facade.referralCode()"
      [state]="referralDialogState()"
      titleKey="pages.order.referral.dialog_title"
      inputLabelKey="pages.order.referral.row_title"
      helperKey="pages.order.referral.dialog_helper"
      cancelKey="pages.order.referral.dialog_cancel"
      applyKey="pages.order.referral.dialog_apply"
      doneKey="pages.order.referral.dialog_done"
      validatingKey="pages.order.referral.validating"
      (applyClicked)="applyReferral($event)"
      (done)="closeReferralDialog()"
      (cancelled)="closeReferralDialog()"
    />
  `,
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
  protected readonly referralDialogVisible = signal(false);

  openPromoDialog(): void {
    this.promoDialogVisible.set(true);
  }

  openReferralDialog(): void {
    this.referralDialogVisible.set(true);
  }

  closePromoDialog(): void {
    this.promoDialogVisible.set(false);
  }

  closeReferralDialog(): void {
    this.referralDialogVisible.set(false);
  }

  /** Bridge dialog `(visibleChange)` into the local signal — types align cleanly. */
  onPromoDialogVisible(visible: boolean): void {
    this.promoDialogVisible.set(visible);
  }

  onReferralDialogVisible(visible: boolean): void {
    this.referralDialogVisible.set(visible);
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

  clearReferral(event: Event): void {
    event.stopPropagation();
    this.facade.clearReferralCode();
  }

  /** Apply tap handler — single backend call, no debounce. */
  async applyPromo(code: string): Promise<void> {
    await this.facade.validatePromoCodeNow(code);
  }

  async applyReferral(code: string): Promise<void> {
    await this.facade.validateReferralCodeNow(code);
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

  protected readonly referralDialogState = computed<CodeDialogResult>(() => {
    const state = this.facade.referralState();
    return referralStateToDialog(state, this.translate);
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
   * Best-wins grand total. Subtracts the effective discount (currently just
   * promo; tier preview will fold in when its client-side path ships). Backend
   * recomputes server-side at order-create time so this is display-only.
   */
  protected readonly grandTotal = computed(() => {
    const subtotal = this.facade.totalPrice();
    const discount = this.facade.effectivePromoDiscount();
    return Math.max(0, subtotal - discount);
  });

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

function referralStateToDialog(
  state: ReferralUiState,
  translate: TranslateService,
): CodeDialogResult {
  switch (state.kind) {
    case 'idle':
      return { kind: 'idle' };
    case 'validating':
      return { kind: 'validating' };
    case 'valid': {
      const name = state.referrerFirstName?.trim();
      const messageKey = name
        ? 'pages.order.referral.dialog_success_named'
        : 'pages.order.referral.dialog_success';
      return {
        kind: 'valid',
        successMessage: translate.instant(messageKey, { name: name ?? '' }),
      };
    }
    case 'invalid': {
      const key =
        REFERRAL_ERROR_KEYS[state.error ?? ''] ?? 'pages.order.referral.error_generic';
      return { kind: 'invalid', errorMessage: translate.instant(key) };
    }
  }
}
