import { computed, effect, inject, Injectable, Injector, PLATFORM_ID, signal, untracked } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddressDto,
  CategoryDto,
  CreateOrderCommand,
  CreateOrderResponse,
  CustomerAddress,
  CustomerAuthService,
  ConsentType,
  CustomerClient,
  UserConsentDto,
  ExtraListItem,
  PackageListItem,
  PaymentType,
  QuoteOrderResponse,
  ServiceListItem,
  QuotePlusSavingsQuery,
} from '@cleansia/customer-services';
import {
  loadCustomerCurrencies,
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerDefaultCurrencyCode,
  selectCustomerPackages,
  selectCustomerPackagesCatalogue,
  selectCustomerServices,
  selectCustomerServicesCatalogue,
  selectMarketCountryId,
  selectMarkets,
} from '@cleansia/customer-stores';
import { GuestOrderService } from '@cleansia-customer/orders';
import {
  CleansiaCustomerRoute,
  extractApiErrorCode,
  marketCountryOptions,
  SnackbarService,
} from '@cleansia/services';
import { CashEligibility, cashIsRefused, resolveCashEligibility } from '@cleansia/models';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, finalize, map, of, takeUntil } from 'rxjs';
import { OrderMembershipFacade } from './order-membership.facade';
import { OrderPreferredCleanerFacade } from './order-preferred-cleaner.facade';
import { OrderPricingFacade } from './order-pricing.facade';
import { OrderPromoFacade } from './order-promo.facade';
import { OrderSavedAddressFacade } from './order-saved-address.facade';
import { OrderServiceAreaFacade } from './order-service-area.facade';
import {
  ORDER_WIZARD_INITIAL_DATA,
  OrderWizardFormData,
  PromoCodeUiState,
  RebookParams,
  cashReasonCopy,
} from './order-wizard.models';

const PAYMENT_STEP = 3;
/** The index of the Plus step in `steps`. */
const PLUS_STEP = 4;

@Injectable()
export class OrderWizardFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly pricing = inject(OrderPricingFacade);
  private readonly promo = inject(OrderPromoFacade);
  private readonly serviceArea = inject(OrderServiceAreaFacade);
  private readonly savedAddress = inject(OrderSavedAddressFacade);
  private readonly membership = inject(OrderMembershipFacade);
  private readonly preferredCleaner = inject(OrderPreferredCleanerFacade);
  private readonly injector = inject(Injector);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  isAuthenticated = signal(false);

  /**
   * Whether this account has ALREADY granted the two consents the review step's
   * tick asks for — Terms of Service and Privacy Policy — at sign-up or on an
   * earlier booking.
   *
   * Owner ruling, 2026-09-03: a signed-in customer who accepted at registration
   * is asked again on every order, and re-consenting to the same two documents
   * is noise, not protection. The tick stays for GUESTS, who have no account
   * and therefore no consent on record — a booking does not require one.
   *
   * Cookie consent is deliberately NOT part of this: cookies are about storage
   * and tracking, and neither consent substitutes for the other.
   *
   * Defaults to false, so the tick is shown whenever this could not be
   * established — a consent that might not exist is asked for.
   */
  readonly alreadyConsented = signal(false);

  services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });
  packages = toSignal(this.store.select(selectCustomerPackages), {
    initialValue: [] as PackageListItem[],
  });
  private readonly defaultCurrencyCode = toSignal(
    this.store.select(selectCustomerDefaultCurrencyCode),
    { initialValue: null },
  );
  // Anonymous catalog of bookable extras, read with the rest of the catalogue for the address's
  // country. Best-effort: if the call fails the wizard still works, the extras section just
  // stays empty (same approach the mobile app uses).
  extras = signal<ExtraListItem[]>([]);

  // ─── Saved-address management ───────────────────────────────────
  //
  // Selection + persisting a new saved address live in OrderSavedAddressFacade,
  // provided alongside this facade on the component. We re-expose its surface so
  // the template/component keep reading the wizard facade.
  readonly savedAddresses = this.savedAddress.savedAddresses;
  readonly selectedSavedAddressId = this.savedAddress.selectedSavedAddressId;

  activeStep = signal(0);
  formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA });
  submitting = signal(false);

  /**
   * Mobile parity: services step shows a chip row to filter the services list
   * by category. `null` = "All" (no filter). Filter is purely visual — selected
   * service IDs in the wizard form data persist across filter changes.
   */
  selectedCategorySlug = signal<string | null>(null);

  /**
   * Distinct categories derived from the loaded services, keyed by `slug` and
   * sorted by the backend-provided `displayOrder`. Categories come from the
   * Service DTO (CategoryDto) — no separate API call needed.
   */
  categories = computed<CategoryDto[]>(() => {
    const seen = new Set<string>();
    const result: CategoryDto[] = [];
    for (const svc of this.services()) {
      const cat = svc.category;
      if (!cat || !cat.slug || seen.has(cat.slug)) continue;
      seen.add(cat.slug);
      result.push(cat);
    }
    return result.sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
  });

  /** Services filtered by the active category chip; "All" returns the full list. */
  filteredServices = computed<ServiceListItem[]>(() => {
    const slug = this.selectedCategorySlug();
    const all = this.services();
    if (!slug) return all;
    return all.filter((s) => s.category?.slug === slug);
  });

  setCategory(slug: string | null): void {
    this.selectedCategorySlug.set(slug);
  }

  steps = [
    'pages.order.steps.services',
    'pages.order.steps.address',
    'pages.order.steps.datetime',
    'pages.order.steps.payment',
    'pages.order.steps.plus',
    'pages.order.steps.summary',
  ];

  stepIcons = [
    'pi pi-list',
    'pi pi-map-marker',
    'pi pi-calendar',
    'pi pi-credit-card',
    'pi pi-star',
    'pi pi-check-circle',
  ];

  // ─── Live quote (server-authoritative pricing) ──────────────────
  //
  // The quote engine lives in OrderPricingFacade, provided alongside this
  // facade on the component. We re-expose its surface so the template/
  // summary-step keep reading the wizard facade.
  readonly quote = this.pricing.quote;
  /**
   * The currency a figure with no payload of its own is printed in: the quote's once there is
   * one, and the platform default before that. A catalogue item carries its own code and is
   * labelled from it. Null until either is known, which prints a bare number rather than a guess.
   */
  readonly currencyCode = computed<string | null>(
    () => this.quote()?.currencyCode || this.defaultCurrencyCode(),
  );

  private readonly marketCountryId = toSignal(this.store.select(selectMarketCountryId), {
    initialValue: null,
  });
  private readonly markets = toSignal(this.store.select(selectMarkets), { initialValue: [] });
  private readonly language = toSignal(
    this.translate.onLangChange.pipe(map(({ lang }) => lang)),
    { initialValue: this.translate.currentLang || this.translate.getDefaultLang() },
  );
  /** The address country picker lists the market directory (ADR-0058 D1). */
  readonly countryOptions = computed(() => marketCountryOptions(this.markets(), this.language()));
  /**
   * The country the address is in, which decides the currency the booking is charged in and what
   * the catalogue and the quote are priced for: the address's own once it names one, and the
   * chosen market's until then (ADR-0058 D4). The picker shows this value, and a market chosen
   * after the address named a country does not touch the booking.
   */
  readonly addressCountryId = computed<string | null>(
    () => this.formData().address.countryId || this.marketCountryId(),
  );
  /** The country the catalogue was last read for, so a same-country address edit re-reads nothing. */
  private catalogueCountryId: string | null = null;
  readonly quoting = this.pricing.quoting;
  readonly totalPrice = this.pricing.totalPrice;
  readonly preSurchargeSubtotal = this.pricing.preSurchargeSubtotal;
  readonly expressSurchargeApplied = this.pricing.expressSurchargeApplied;
  readonly expressSurchargeWaived = this.pricing.expressSurchargeWaived;
  readonly expressSurcharge = this.pricing.expressSurcharge;
  readonly displayedTotalPrice = this.pricing.displayedTotalPrice;
  readonly tierDiscount = this.pricing.tierDiscount;
  readonly membershipDiscount = this.pricing.membershipDiscount;
  readonly effectiveDiscount = this.pricing.effectiveDiscount;

  /**
   * Whether this booking may be paid in cash, from the live session and the crew the server quoted
   * for the selection on screen. A quote for an earlier selection says nothing about this one.
   */
  readonly cashEligibility = computed<CashEligibility>(() =>
    resolveCashEligibility(
      this.authService.isLoggedIn(),
      this.pricing.cachedQuoteMatchesCurrentState()
        ? (this.quote()?.requiredEmployees ?? null)
        : null,
    ),
  );
  readonly cashSelectable = computed(() => this.cashEligibility().kind === 'available');
  readonly cashReason = computed(() => cashReasonCopy(this.cashEligibility()));
  readonly cashNeedsAccount = computed(() => this.cashEligibility().kind === 'needs_account');
  /** A cash choice was taken away because it stopped being allowed; cleared by the next choice. */
  readonly cashCleared = signal(false);
  /** Said only while cash is still not available; a booking that allows it again needs no warning. */
  readonly cashClearedNotice = computed(() => this.cashCleared() && !this.cashSelectable());
  // Credit: the balance, the slice this booking takes, and what the card is left to pay.
  // Owner ruling 2026-09-05 — applied automatically, and never the whole booking.
  readonly creditBalance = this.pricing.creditBalance;
  readonly creditApplied = this.pricing.creditApplied;
  readonly amountDueOnCard = this.pricing.amountDueOnCard;

  // ─── Membership (free-cancellation window + express waiver) ─────
  //
  // One /Membership/Mine read for the whole wizard, owned by OrderMembershipFacade and
  // re-exposed here so the slot grid and the summary step both read the wizard facade.
  readonly plusFreeCancellationHours = this.membership.freeCancellationWindowHours;
  readonly expressUpgradesRemaining = this.membership.expressUpgradesRemaining;
  readonly activeMembership = this.membership.membership;
  readonly plans = this.membership.plans;
  readonly plusUnavailable = this.membership.plusUnavailable;
  readonly plusSavings = this.membership.plusSavings;
  readonly expressWaiverAvailable = this.membership.expressWaiverAvailable;
  readonly expressWaiverExhausted = this.membership.expressWaiverExhausted;
  readonly expressWaiverPendingTrial = this.membership.expressWaiverPendingTrial;

  // ─── Preferred cleaner (Plus) ───────────────────────────────────
  //
  // The roster and its slot answer live in OrderPreferredCleanerFacade, provided alongside this
  // facade on the component. Re-exposed so the summary step keeps reading the wizard facade.
  readonly preferredCleanerVisible = this.preferredCleaner.visible;
  readonly preferredCleanerLoading = this.preferredCleaner.loading;
  readonly preferredCleanerOptions = this.preferredCleaner.options;

  // ─── Service-area (city-serviced) check ─────────────────────────
  //
  // The client-side service-area lookup lives in OrderServiceAreaFacade,
  // provided alongside this facade on the component. We re-expose its signal
  // so the template keeps reading the wizard facade.
  readonly cityServiced = this.serviceArea.cityServiced;

  loadPlans(): void {
    this.membership.loadPlans();
  }

  loadPlusSavings(query: QuotePlusSavingsQuery): void {
    this.membership.loadPlusSavings(query);
  }

  constructor() {
    super();
    this.pricing.connect({
      formData: this.formData,
      promoDiscount: this.promo.effectivePromoDiscount,
      marketCountryId: this.marketCountryId,
    });
    this.promo.connect({
      preSurchargeSubtotal: this.preSurchargeSubtotal,
      currencyId: computed(() => this.quote()?.currencyId ?? null),
      persistPromoCode: (value) => this.updateFormData({ promoCode: value }),
    });
    this.serviceArea.connect({
      currentAddress: () => this.formData().address,
    });
    this.savedAddress.connect({
      currentFormData: () => this.formData(),
      patchFormData: (partial) => this.updateFormData(partial),
    });
    this.preferredCleaner.connect({
      isAuthenticated: () => this.isAuthenticated(),
      hasMembership: () => this.membership.membership()?.hasMembership === true,
      currentFormData: () => this.formData(),
      patchFormData: (partial) => this.updateFormData(partial),
    });
    effect(() => {
      if (this.formData().paymentType === PaymentType.Cash && cashIsRefused(this.cashEligibility())) {
        untracked(() => this.dropCash(true));
      }
    });
  }

  selectPaymentType(type: PaymentType): void {
    if (type === PaymentType.Cash && !this.cashSelectable()) return;
    this.cashCleared.set(false);
    this.updateFormData({ paymentType: type });
  }

  /** Never replaced by card: the customer is told and chooses again. */
  private dropCash(announce: boolean): void {
    this.updateFormData({ paymentType: null });
    this.cashCleared.set(true);
    if (announce) this.snackbarService.showInfoTranslated('pages.order.cash_cleared');
  }

  selectPreferredCleaner(employeeId: string | null): void {
    this.preferredCleaner.select(employeeId);
  }

  selectedPreferredCleanerId(): string | null {
    return this.preferredCleaner.selectedEmployeeId();
  }

  /** Delegates to the pricing engine — see OrderPricingFacade.refreshQuoteNow. */
  refreshQuoteNow(): Promise<QuoteOrderResponse | null> {
    return this.pricing.refreshQuoteNow();
  }

  // ─── Promo code validation ───────────────────────────────────
  //
  // The promo validation state machine + its backend call live in
  // OrderPromoFacade, provided alongside this facade on the component. We
  // re-expose its surface so the template/summary-step keep reading the
  // wizard facade.
  readonly promoCode = this.promo.promoCode;
  readonly promoCodeState = this.promo.promoCodeState;

  /**
   * Floor at which the tier discount kicks in (e.g. Silver = 1000 CZK). Used to
   * render a "needs orders above X" hint when the customer's tier discount didn't
   * apply because the subtotal is below it.
   */
  tierDiscountMinOrderAmount = computed(
    () => this.quote()?.tierDiscountMinOrderAmount ?? null,
  );

  /** Promo discount the user just applied via the dialog — see OrderPromoFacade. */
  readonly effectivePromoDiscount = this.promo.effectivePromoDiscount;

  /**
   * Which discount source(s) apply right now. `'combined'` appears when
   * both Plus and tier are non-zero and the promo (if any) is smaller —
   * the sidebar then renders both labels stacked. Single-source kinds
   * render a single row as before.
   */
  appliedDiscountKind = computed<'none' | 'membership' | 'tier' | 'combined' | 'promo'>(() => {
    const m = this.membershipDiscount();
    const t = this.tierDiscount();
    const p = this.effectivePromoDiscount();
    const combined = m + t;
    if (combined === 0 && p === 0) return 'none';
    if (p > combined) return 'promo';
    if (m > 0 && t > 0) return 'combined';
    if (m > 0) return 'membership';
    return 'tier';
  });

  setPromoCode(value: string): void {
    this.promo.setPromoCode(value);
  }

  /** Apply-button handler from the promo dialog — see OrderPromoFacade. */
  validatePromoCodeNow(code: string): Promise<PromoCodeUiState> {
    return this.promo.validatePromoCodeNow(code);
  }

  /** Wipes the applied promo state — used by the row's clear-X button. */
  clearPromoCode(): void {
    this.promo.clearPromoCode();
  }

  initialize(): void {
    this.followAddressCountry();
    this.store.dispatch(loadCustomerCurrencies());
    const loggedIn = this.authService.isLoggedIn();
    this.isAuthenticated.set(loggedIn);
    this.membership.load(loggedIn);

    if (loggedIn) {
      this.loadConsentState();
      if (!this.savedAddressStore.loaded()) {
        this.savedAddressStore.refresh();
      }
      this.customerClient.userClient.getCurrent().pipe(takeUntil(this.destroyed$)).subscribe({
        next: (user) => {
          // PREFILL, not overwrite. This wrote all four fields unconditionally, empty account
          // values included — and an account with no phone number on file is the ordinary case,
          // because nothing in registration asks for one. So a customer who typed their phone into
          // the wizard had it replaced with "" the moment this response landed, and the wizard let
          // them carry on to a submit the server rejected as `customerPhone: ""`.
          //
          // Filling only what is blank is what a prefill means, and it is right in every direction:
          // an account value appears in an empty field, and nothing the customer typed is taken
          // away by a slower request.
          const current = this.formData();
          this.updateFormData({
            customerFirstName: current.customerFirstName || user.firstName || '',
            customerLastName: current.customerLastName || user.lastName || '',
            customerEmail: current.customerEmail || user.email || '',
            customerPhone: current.customerPhone || user.phoneNumber || '',
          });

          const currentAddr = this.formData().address;
          if (!currentAddr.street && !currentAddr.city) {
            const defaultAddr = this.savedAddressStore.defaultAddress();
            if (defaultAddr?.id) {
              this.selectSavedAddress(defaultAddr.id);
            }
          }
        },
      });
    }
  }

  /**
   * The catalogue is priced per market and the server withholds what has no price in the
   * country's currency, so it is read for the chosen market first and again for every country
   * the address names. A basket entry the new list no longer offers would make the server refuse
   * the quote outright, so the basket is trimmed to the new list — with a word to the customer —
   * once that list has landed.
   */
  private followAddressCountry(): void {
    toObservable(this.addressCountryId, { injector: this.injector })
      .pipe(takeUntil(this.destroyed$))
      .subscribe((countryId) => {
        if (countryId !== this.catalogueCountryId) this.loadCatalogue(countryId);
      });
    this.loadCatalogue(this.addressCountryId());

    this.store
      .select(selectCustomerServicesCatalogue)
      .pipe(takeUntil(this.destroyed$))
      .subscribe(({ services, countryId }) => {
        if (!this.pricedForAddress(countryId)) return;
        const offered = new Set(services.map((s) => s.id));
        this.keepSelectedServices((id) => offered.has(id));
      });
    this.store
      .select(selectCustomerPackagesCatalogue)
      .pipe(takeUntil(this.destroyed$))
      .subscribe(({ packages, countryId }) => {
        if (!this.pricedForAddress(countryId)) return;
        const offered = new Set(packages.map((p) => p.id));
        this.keepSelectedPackages((id) => offered.has(id));
      });
  }

  private loadCatalogue(countryId: string | null): void {
    this.catalogueCountryId = countryId;
    this.store.dispatch(loadCustomerServices(countryId));
    this.store.dispatch(loadCustomerPackages(countryId));
    // `?? []` for the same generated-client null as the countries read in `initialize`; spreading
    // null throws "not iterable", so this one takes the whole wizard init down rather than storing
    // a lie — and it does so past the `error` handler, which sees a failed request, not a bad body.
    this.customerClient.extraClient
      .getOverview(countryId ?? undefined)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (extras) => {
          const offered = [...(extras ?? [])].sort(
            (a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0),
          );
          this.extras.set(offered);
          if (!this.pricedForAddress(countryId)) return;
          const slugs = new Set(offered.map((e) => e.slug));
          this.keepSelectedExtras((slug) => slugs.has(slug));
        },
        error: () => this.extras.set([]),
      });
  }

  /** A list priced for the platform default never trims: nothing was ever picked outside it. */
  private pricedForAddress(countryId: string | null): boolean {
    return countryId !== null && countryId === this.addressCountryId();
  }

  private inlineCustomerAddress(address: AddressDto): CustomerAddress {
    const customerAddress = new CustomerAddress(address);
    customerAddress.countryId = this.addressCountryId() ?? '';
    return customerAddress;
  }

  private keepSelectedServices(offered: (id: string) => boolean): void {
    const selected = this.formData().selectedServiceIds;
    const kept = selected.filter(offered);
    if (kept.length === selected.length) return;
    this.updateFormData({ selectedServiceIds: kept });
    this.snackbarService.showInfoTranslated('pages.order.wizard.catalogue_changed_for_country');
  }

  private keepSelectedPackages(offered: (id: string) => boolean): void {
    const selected = this.formData().selectedPackageIds;
    const kept = selected.filter(offered);
    if (kept.length === selected.length) return;
    this.updateFormData({ selectedPackageIds: kept });
    this.snackbarService.showInfoTranslated('pages.order.wizard.catalogue_changed_for_country');
  }

  private keepSelectedExtras(offered: (slug: string) => boolean): void {
    const selected = this.formData().extras;
    const kept = Object.fromEntries(Object.entries(selected).filter(([slug]) => offered(slug)));
    if (Object.keys(kept).length === Object.keys(selected).length) return;
    this.updateFormData({ extras: kept });
    this.snackbarService.showInfoTranslated('pages.order.wizard.catalogue_changed_for_country');
  }

  /**
   * Reads the account's consents once. Both of the two must be granted and
   * neither withdrawn — a withdrawn Privacy Policy consent is not a consent,
   * and asking again is the correct response to one.
   *
   * A failure leaves the flag false, which shows the tick. The safe direction
   * for this switch is always "ask".
   */
  private loadConsentState(): void {
    this.customerClient.gdprClient
      .consentsGet()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as UserConsentDto[])),
      )
      .subscribe((consents) => {
        // `?? []` for the same generated-client null as in `initialize` — the `catchError` above
        // covers a failed request, not a 200 whose body is not an array. Coalesced once, because
        // `granted` runs over it twice.
        const onRecord = consents ?? [];
        const granted = (type: ConsentType) =>
          onRecord.some(
            (c) => c.consentType === type && c.isGranted && !c.withdrawnAt,
          );
        this.alreadyConsented.set(
          granted(ConsentType.TermsOfService) && granted(ConsentType.PrivacyPolicy),
        );
      });
  }

  selectSavedAddress(addressId: string): void {
    this.savedAddress.selectSavedAddress(addressId);
  }

  updateFormData(partial: Partial<OrderWizardFormData>): void {
    this.formData.update((current) => ({ ...current, ...partial }));
    // City / country can change via multiple paths (Mapbox pick, manual
    // edit, saved-address selection). Centralise the service-area check
    // here so every path triggers it uniformly.
    if (partial.address !== undefined) {
      this.serviceArea.refreshCheck();
    }
  }

  /**
   * Toggle a catalog extra by slug. Selected slugs go into the
   * `extras: Record<slug, boolean>` form field; the quote watcher debounces
   * and refreshes the price as soon as the snapshot stabilises.
   */
  toggleExtra(slug: string): void {
    const current = this.formData().extras;
    const next = { ...current };
    if (current[slug]) {
      delete next[slug];
    } else {
      next[slug] = true;
    }
    this.updateFormData({ extras: next });
  }

  updateAddressFromForm(next: AddressDto): void {
    this.savedAddress.updateAddressFromForm(next);
  }

  /**
   * Called when the user picks an address from Mapbox autocomplete.
   * Populates the address fields AND captures lat/lng for the eventual save.
   */
  applyAddressSuggestion(suggestion: {
    street: string;
    city: string;
    zipCode: string;
    latitude: number;
    longitude: number;
  }): void {
    this.savedAddress.applyAddressSuggestion(suggestion);
  }

  prefillFromRebook(params: RebookParams): string[] {
    const availableServices = this.services();
    const availablePackages = this.packages();

    const availableServiceIds = availableServices.map((s) => s.id);
    const availablePackageIds = availablePackages.map((p) => p.id);

    const validServiceIds = params.selectedServiceIds.filter((id) =>
      availableServiceIds.includes(id)
    );
    const validPackageIds = params.selectedPackageIds.filter((id) =>
      availablePackageIds.includes(id)
    );

    const unavailableItems: string[] = [];
    params.selectedServiceIds.forEach((id, i) => {
      if (!availableServiceIds.includes(id)) {
        unavailableItems.push(params.selectedServiceNames[i] || id);
      }
    });
    params.selectedPackageIds.forEach((id, i) => {
      if (!availablePackageIds.includes(id)) {
        unavailableItems.push(params.selectedPackageNames[i] || id);
      }
    });

    const update: Partial<OrderWizardFormData> = {
      selectedServiceIds: validServiceIds,
      selectedPackageIds: validPackageIds,
      rooms: params.rooms,
      bathrooms: params.bathrooms,
    };

    if (params.address) {
      update.address = new AddressDto(params.address);
    }

    this.updateFormData(update);

    return unavailableItems;
  }

  nextStep(): void {
    const next = this.stepFrom(this.activeStep(), 1);
    if (next !== null) {
      this.activeStep.set(next);
      this.onStepEntered();
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  prevStep(): void {
    const previous = this.stepFrom(this.activeStep(), -1);
    if (previous !== null) {
      this.activeStep.set(previous);
      this.onStepEntered();
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  /**
   * The neighbouring step in one direction, or null at the ends. The Plus step is walked past
   * when the chosen market sells no plan: a checkout step whose only content is a decline row is
   * a dead page (ADR-0059 D3). Reached directly it still renders, saying so.
   */
  private stepFrom(step: number, direction: 1 | -1): number | null {
    let candidate = step + direction;
    if (candidate === PLUS_STEP && this.plusUnavailable()) candidate += direction;
    return candidate >= 0 && candidate < this.steps.length ? candidate : null;
  }

  /**
   * Deliberately ungated, in both directions. Looking ahead at a step you have not filled in is not
   * a mistake to prevent — the existing specs pin that freedom — and SUBMIT is where an incomplete
   * order has to be caught, because that is the only moment it can do harm. See `submitOrder`.
   */
  goToStep(step: number): void {
    if (step >= 0 && step < this.steps.length) {
      this.activeStep.set(step);
      this.onStepEntered();
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  /**
   * The roster is re-read on every entry to the summary step rather than once, because its
   * availability answer is about the slot — and the slot is two steps behind, editable, and
   * routinely changed after a first look at the summary.
   */
  private onStepEntered(): void {
    if (this.activeStep() === this.steps.length - 1) {
      this.preferredCleaner.refresh();
    }
  }

  private readonly emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  private readonly phoneRegex = /^[+]?[\d\s()-]{6,20}$/;
  private readonly zipRegex = /^[\d\s-]{3,20}$/;

  isSavedAddressSelected(): boolean {
    return this.savedAddress.isSavedAddressSelected();
  }

  /**
   * Why this step cannot be left yet, as translation keys, most important first.
   * Empty means it can.
   *
   * `canProceed()` is derived from this rather than checking the same conditions
   * a second time: a button whose disabled state and whose explanation are
   * computed separately drift apart, and the drift is invisible until someone
   * is staring at an inert button with nothing to fix.
   */
  missingReasons(step: number = this.activeStep()): string[] {
    const data = this.formData();
    const reasons: string[] = [];

    switch (step) {
      case 0:
        if (data.selectedServiceIds.length === 0 && data.selectedPackageIds.length === 0) {
          reasons.push('pages.order.missing.services');
        }
        break;

      case 1: {
        // Saved address: the server already validated the record, so only
        // non-emptiness is checked. Custom address: it must have come from a
        // suggestion pick, which is the only thing that sets lat/lng — typing
        // into the field alone never produces a bookable address.
        const usingSaved = this.isAuthenticated() && this.isSavedAddressSelected();
        const fieldsValid = !!(
          data.address.street &&
          data.address.street.length >= 5 &&
          data.address.street.length <= 255 &&
          data.address.city &&
          data.address.city.length >= 2 &&
          data.address.city.length <= 100 &&
          data.address.zipCode &&
          this.zipRegex.test(data.address.zipCode)
        );
        // Coordinates are required of a LOOKUP address, because a half-finished
        // pick has the words without the place. A typed address is a different
        // promise: the customer said the lookup could not find it, and the
        // server geocodes it on submit.
        const addressValid = usingSaved
          ? !!(data.address.street && data.address.city && data.address.zipCode)
          : fieldsValid &&
            (data.addressEnteredManually ||
              (data.addressLatitude != null && data.addressLongitude != null));
        if (!addressValid) {
          reasons.push(
            data.addressEnteredManually
              ? 'pages.order.missing.address_fields'
              : 'pages.order.missing.address'
          );
        }

        // Only an explicit rejection blocks. 'pending' / 'error' / 'idle' pass
        // through — the backend re-validates on submit, and a network failure or
        // a check that has not fired yet is not the customer's problem.
        if (this.cityServiced() === 'rejected') {
          reasons.push('api.service_area.city_not_serviced');
        }

        if (!(data.customerFirstName && data.customerFirstName.length >= 2 && data.customerFirstName.length <= 50)) {
          reasons.push('pages.order.missing.first_name');
        }
        if (!(data.customerLastName && data.customerLastName.length >= 2 && data.customerLastName.length <= 50)) {
          reasons.push('pages.order.missing.last_name');
        }
        if (!(data.customerEmail && this.emailRegex.test(data.customerEmail) && data.customerEmail.length <= 50)) {
          reasons.push('pages.order.missing.email');
        }
        if (!(data.customerPhone && this.phoneRegex.test(data.customerPhone.replace(/\s/g, '')))) {
          reasons.push('pages.order.missing.phone');
        }
        break;
      }

      case 2:
        if (!data.cleaningDate) {
          reasons.push('pages.order.missing.date');
        }
        break;

      case PAYMENT_STEP:
        if (
          data.paymentType === null ||
          (data.paymentType === PaymentType.Cash && cashIsRefused(this.cashEligibility()))
        ) {
          reasons.push('pages.order.missing.payment');
        }
        break;

      default:
        break;
    }

    return reasons;
  }

  canProceed(): boolean {
    return this.missingReasons().length === 0;
  }

  /**
   * True once the order has been created. The wizard parks its basket on the way out; a basket that
   * has just been paid for must not be offered back on the next visit.
   */
  readonly orderPlaced = signal(false);

  /**
   * Everything still missing anywhere in the wizard, and the first step that is missing it.
   *
   * `missingReasons` answers for ONE step, which is all a Continue button needs — and it was all
   * anything ever asked. `goToStep` sets the step with no gate, so the review screen was reachable
   * over an unsatisfied step, and `submitOrder` checked only the cleaning date. An account with no
   * phone number therefore reached Stripe's door and came back with
   * `{ NotEmptyValidator: "common.required" }`, which named neither the field nor a way forward.
   */
  firstIncompleteStep(): { step: number; reasons: string[] } | null {
    for (let step = 0; step < this.steps.length; step++) {
      const reasons = this.missingReasons(step);
      if (reasons.length > 0) return { step, reasons };
    }
    return null;
  }

  saveCurrentAddressAsSaved(label: string): Promise<boolean> {
    return this.savedAddress.saveCurrentAddressAsSaved(label);
  }

  /**
   * `termsAccepted` is the ONE client-asserted member on the create command: the server cannot
   * observe a tick, so it records the customer's own assertion against themselves (ADR-0062 D4).
   * It is sent only when the box was shown AND ticked; an account that already consented sees no
   * box and asserts nothing new.
   */
  async submitOrder(
    saveAddress?: { label: string } | null,
    termsAccepted = false,
  ): Promise<void> {
    const data = this.formData();
    if (!data.cleaningDate) return;

    // The LAST gate, and until now the only one that was not here: submit checked the date alone,
    // so anything the per-step gates never got to ask about went to the server and came back as a
    // 400 the customer could do nothing with. Sends them to the step that is short, rather than
    // failing where they cannot see the field.
    const blocked = this.firstIncompleteStep();
    if (blocked) {
      this.activeStep.set(blocked.step);
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
      this.snackbarService.showError(this.translate.instant(blocked.reasons[0]));
      return;
    }

    if (saveAddress && !this.selectedSavedAddressId()) {
      const saved = await this.saveCurrentAddressAsSaved(saveAddress.label);
      if (!saved) return;
    }

    this.submitting.set(true);

    // Resolve the authoritative quote BEFORE we build the command. If the
    // cache matches the current selection, reuse it. Otherwise fetch a fresh
    // one synchronously — the backend validator will reject /Create if the
    // total doesn't match its calculation, so we must send the server's number.
    let quoted = this.quote();
    if (!quoted || !this.pricing.cachedQuoteMatchesCurrentState()) {
      quoted = await this.refreshQuoteNow();
    }
    if (!quoted) {
      this.submitting.set(false);
      this.snackbarService.showError(
        this.translate.instant('pages.order.quote_failed'),
      );
      return;
    }

    const paymentType = this.formData().paymentType;
    if (
      paymentType === null ||
      (paymentType === PaymentType.Cash && !this.cashSelectable())
    ) {
      this.submitting.set(false);
      if (paymentType === PaymentType.Cash) this.dropCash(true);
      this.goToPaymentStep();
      return;
    }

    const selectedDate = new Date(data.cleaningDate);
    const [hours, minutes] = data.cleaningTime.split(':').map(Number);
    // Build the slot in the user's LOCAL timezone — `cleaningTime` is the
    // local-clock hour the customer picked ("14:00 Prague"). Using
    // `Date.UTC(...)` would treat 14:00 as 14:00Z, which lands at 16:00 Prague
    // in summer and silently shifts the slot 1–2 hours into the future. The
    // backend then computes `(cleaningUtc - nowUtc).TotalHours` against the
    // wrong instant and may decide the slot is no longer in the 2–4h express
    // window — surcharge gets skipped, customer pays the bare price even
    // though the time-slot picker said "Express +20%".
    const cleaningDate = new Date(
      selectedDate.getFullYear(),
      selectedDate.getMonth(),
      selectedDate.getDate(),
      hours, minutes, 0, 0,
    );

    const savedId = this.selectedSavedAddressId();
    // Only forward the promo code when the live validation says it's good.
    // Sending an unverified or invalid code wastes a backend round-trip and
    // produces a misleading 4xx; the backend re-validates regardless.
    const promoState = this.promoCodeState();
    const trimmedPromo = this.promoCode().trim();
    const promoCodeToSend =
      promoState.kind === 'valid' && trimmedPromo
        ? trimmedPromo.toUpperCase()
        : undefined;
    const command = new CreateOrderCommand();
    command.customerName =
      `${data.customerFirstName} ${data.customerLastName}`.trim();
    command.customerEmail = data.customerEmail;
    command.customerPhone = data.customerPhone;
    // Backend validator is XOR: send savedAddressId OR customerAddress, never both. The inline
    // address carries the country the picker showed and the quote was priced for.
    command.customerAddress = savedId ? undefined : this.inlineCustomerAddress(data.address);
    command.savedAddressId = savedId ?? undefined;
    command.selectedServiceIds = data.selectedServiceIds;
    command.selectedPackageIds = data.selectedPackageIds;
    command.rooms = data.rooms;
    command.bathrooms = data.bathrooms;
    command.extras = data.extras;
    command.cleaningDate = cleaningDate;
    command.paymentType = paymentType;
    // The currency the server resolved for the address's country, echoed so the create prices in
    // the same one the quote did.
    command.currencyId = quoted.currencyId;
    // Send the server-quoted total unchanged — it already includes any
    // express surcharge for the quoted slot. `CreateOrder.PriceMatchesAsync`
    // validates against the same calculator result (`result.TotalPrice ==
    // command.TotalPrice`), exact decimal match, so any client-side price
    // math here would be rejected.
    command.totalPrice = quoted.totalPrice;
    command.language =
      this.translate.currentLang || this.translate.getDefaultLang();
    command.promoCode = promoCodeToSend;
    command.termsAccepted = termsAccepted ? true : undefined;
    // Empty becomes undefined rather than '': the backend treats null and empty
    // alike, and undefined keeps the property out of the JSON entirely. Trimmed
    // because a whitespace-only note is not a note.
    command.specialInstructions = data.specialInstructions.trim() || undefined;
    command.accessInstructions = data.entryInstructions.trim() || undefined;
    // A house has neither, and the step hides both — but a customer who filled
    // them in and then switched to "house" would otherwise still send them.
    const isFlat = data.propertyType === 'flat';
    command.customerFloor = (isFlat && data.customerFloor.trim()) || undefined;
    command.customerApartment = (isFlat && data.customerApartment.trim()) || undefined;
    command.accessMode = data.accessMode || undefined;
    // The picker only ever offers cleaners the roster returned, but the entitlement, the eligibility
    // and the seat are all re-decided server-side; an id here asks, it does not reserve.
    command.preferredEmployeeId = data.preferredEmployeeId ?? undefined;
    // Deliberately unset: `referralCode` is a signup-only benefit the checkout wizard never
    // populates. It stays off the JSON.

    if (paymentType === PaymentType.Card) {
      this.customerClient.paymentClient
        .createOrder(command)
        .pipe(
          takeUntil(this.destroyed$),
          catchError((error: unknown) => {
            this.onCreateRefused(error);
            return of(null);
          }),
          finalize(() => this.submitting.set(false)),
        )
        .subscribe((response) => {
          if (!response) return;
          this.rememberGuestBooking(response);
          this.orderPlaced.set(true);
          if (response.stripeSessionId) {
            if (this.isBrowser) window.location.href = response.stripeSessionId;
          } else {
            this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS], {
              queryParams: { type: 'card', orderId: response.id },
            });
          }
        });
    } else {
      this.customerClient.orderClient
        .createOrder(command)
        .pipe(
          takeUntil(this.destroyed$),
          catchError((error: unknown) => {
            this.onCreateRefused(error);
            return of(null);
          }),
          finalize(() => this.submitting.set(false)),
        )
        .subscribe((response) => {
          if (!response) return;
          this.rememberGuestBooking(response);
          this.orderPlaced.set(true);
          this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS], {
            queryParams: { type: 'cash', orderId: response.id },
          });
        });
    }
  }

  /**
   * The create response is the only moment a guest's browser learns the booking's access token
   * without waiting for the e-mail; the confirmation page reads the booking back with it. An
   * account booking answers with none — its owner signs in instead.
   */
  private rememberGuestBooking(response: CreateOrderResponse): void {
    if (response.id && response.guestAccessToken) {
      this.guestOrderService.save(response.id, response.guestAccessToken);
    }
  }

  /**
   * A promo the server will not honour refuses the whole create rather than booking at full
   * price. The interceptor has already toasted which promo rule refused it, and a second, generic
   * toast would replace that sentence — so this one only takes the code off the order, which is
   * what lets the customer submit again. Refused cash is handled the same way: taken off, and the
   * customer sent back to choose how to pay.
   */
  private onCreateRefused(error: unknown): void {
    const code = extractApiErrorCode(error);
    if (code?.startsWith('promo.')) {
      this.promo.clearPromoCode();
      return;
    }
    if (code === 'order.cash_not_available') {
      this.dropCash(false);
      this.goToPaymentStep();
      return;
    }
    this.snackbarService.showError(this.translate.instant('pages.order.submit_error'));
  }

  private goToPaymentStep(): void {
    this.activeStep.set(PAYMENT_STEP);
    if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
