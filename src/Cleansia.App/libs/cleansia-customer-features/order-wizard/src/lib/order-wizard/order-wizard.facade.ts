import { inject, Injectable, PLATFORM_ID, signal, computed } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddressDto,
  CategoryDto,
  CountryListItem,
  CreateOrderCommand,
  CustomerAddress,
  CustomerAuthService,
  CustomerClient,
  ExtraListItem,
  PackageListItem,
  PaymentType,
  QuoteOrderResponse,
  ServiceListItem,
} from '@cleansia/customer-services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { GuestOrderService } from '@cleansia-customer/orders';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { OrderPricingFacade } from './order-pricing.facade';
import { OrderPromoFacade } from './order-promo.facade';
import { OrderSavedAddressFacade } from './order-saved-address.facade';
import { OrderServiceAreaFacade } from './order-service-area.facade';
import {
  ORDER_WIZARD_INITIAL_DATA,
  OrderWizardFormData,
  PromoCodeUiState,
  RebookParams,
} from './order-wizard.models';

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
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  isAuthenticated = signal(false);

  services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });
  packages = toSignal(this.store.select(selectCustomerPackages), {
    initialValue: [] as PackageListItem[],
  });
  countries = signal<CountryListItem[]>([]);
  // Anonymous catalog of bookable extras. Loaded once when the facade
  // initialises; rendered as a toggle list on the summary step. Best-effort:
  // if the call fails the wizard still works, the extras section just stays
  // empty (same approach the mobile app uses).
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
    'pages.order.steps.summary',
  ];

  stepIcons = [
    'pi pi-list',
    'pi pi-map-marker',
    'pi pi-calendar',
    'pi pi-credit-card',
    'pi pi-check-circle',
  ];

  // ─── Live quote (server-authoritative pricing) ──────────────────
  //
  // The quote engine lives in OrderPricingFacade, provided alongside this
  // facade on the component. We re-expose its surface so the template/
  // summary-step keep reading the wizard facade.
  readonly quote = this.pricing.quote;
  readonly quoting = this.pricing.quoting;
  readonly totalPrice = this.pricing.totalPrice;
  readonly preSurchargeSubtotal = this.pricing.preSurchargeSubtotal;
  readonly expressSurchargeApplied = this.pricing.expressSurchargeApplied;
  readonly expressSurcharge = this.pricing.expressSurcharge;
  readonly displayedTotalPrice = this.pricing.displayedTotalPrice;

  // ─── Service-area (city-serviced) check ─────────────────────────
  //
  // The client-side service-area lookup lives in OrderServiceAreaFacade,
  // provided alongside this facade on the component. We re-expose its signal
  // so the template keeps reading the wizard facade.
  readonly cityServiced = this.serviceArea.cityServiced;

  constructor() {
    super();
    this.pricing.connect({
      formData: this.formData,
      effectiveDiscount: this.effectiveDiscount,
    });
    this.promo.connect({
      displayedTotalPrice: this.displayedTotalPrice,
      persistPromoCode: (value) => this.updateFormData({ promoCode: value }),
    });
    this.serviceArea.connect({
      currentAddress: () => this.formData().address,
    });
    this.savedAddress.connect({
      currentFormData: () => this.formData(),
      patchFormData: (partial) => this.updateFormData(partial),
    });
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
   * Server-resolved tier discount preview from the live quote (anonymous quotes return 0).
   */
  tierDiscount = computed(() => this.quote()?.tierDiscountAmount ?? 0);

  /**
   * Server-resolved Cleansia Plus membership discount preview from the live quote.
   */
  membershipDiscount = computed(() => this.quote()?.membershipDiscountAmount ?? 0);

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
   * LOY-003 — effective discount displayed to the user. Plus + tier are
   * additive (server already returns both amounts on the same quote,
   * capped at 12% combined). Promo replaces the combined pair when larger.
   * Mirrors backend `OrderFactory.ResolveLoy003Discount`.
   */
  effectiveDiscount = computed(() => {
    const combined = this.membershipDiscount() + this.tierDiscount();
    return Math.max(combined, this.effectivePromoDiscount());
  });

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
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
    // `getServiced` returns only countries the company operates in. The old
    // `getOverview` call alphabetically returned the full catalog, so the
    // auto-select-first-country fallback silently picked Argentina for
    // every CZ booking — the address persisted with CountryId=Argentina and
    // the backend now (rightly) rejects that. Service-area work in
    // planning/active/service-areas.md.
    this.customerClient.countryClient.getServiced().pipe(takeUntil(this.destroyed$)).subscribe({
      next: (countries) => {
        this.countries.set(countries);
        // Auto-select country ONLY when there's exactly one served — otherwise
        // require the user to pick. With multiple served countries we'd hit
        // the same silent-default bug if we auto-picked here, just with a
        // different country.
        if (countries.length === 1 && !this.formData().address.countryId) {
          this.updateFormData({
            address: new AddressDto({
              ...this.formData().address,
              countryId: countries[0].id ?? '',
            }),
          });
        }
      },
    });
    // Best-effort load — empty catalog just hides the extras section.
    this.customerClient.extraClient.getOverview().pipe(takeUntil(this.destroyed$)).subscribe({
      next: (extras) => this.extras.set([...extras].sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0))),
      error: () => this.extras.set([]),
    });

    const loggedIn = this.authService.isLoggedIn();
    this.isAuthenticated.set(loggedIn);

    if (loggedIn) {
      if (!this.savedAddressStore.loaded()) {
        this.savedAddressStore.refresh();
      }
      this.customerClient.userClient.getCurrent().pipe(takeUntil(this.destroyed$)).subscribe({
        next: (user) => {
          this.updateFormData({
            customerFirstName: user.firstName ?? '',
            customerLastName: user.lastName ?? '',
            customerEmail: user.email ?? '',
            customerPhone: user.phoneNumber ?? '',
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
    if (this.activeStep() < this.steps.length - 1) {
      this.activeStep.update((s) => s + 1);
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  prevStep(): void {
    if (this.activeStep() > 0) {
      this.activeStep.update((s) => s - 1);
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  goToStep(step: number): void {
    if (step >= 0 && step < this.steps.length) {
      this.activeStep.set(step);
      if (this.isBrowser) window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  private readonly emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  private readonly phoneRegex = /^[+]?[\d\s()-]{6,20}$/;
  private readonly zipRegex = /^[\d\s-]{3,20}$/;

  isSavedAddressSelected(): boolean {
    return this.savedAddress.isSavedAddressSelected();
  }

  canProceed(): boolean {
    const data = this.formData();
    switch (this.activeStep()) {
      case 0:
        return (
          data.selectedServiceIds.length > 0 ||
          data.selectedPackageIds.length > 0
        );
      case 1: {
        const phoneValid = !!(data.customerPhone && this.phoneRegex.test(data.customerPhone.replace(/\s/g, '')));

        // Saved address: server already validated the record; just ensure fields are non-empty.
        // Saved addresses always carry lat/lng post backend hardening, so no extra geo check needed.
        // Custom address: must come from a Mapbox pick — i.e. lat/lng are non-null. Editable
        // street/city/zip inputs were removed; the only path to populate them is `applyAddressSuggestion`.
        const usingSaved = this.isAuthenticated() && this.isSavedAddressSelected();
        const addressValid = usingSaved
          ? !!(data.address.street && data.address.city && data.address.zipCode)
          : !!(
              data.address.street &&
              data.address.street.length >= 5 &&
              data.address.street.length <= 255 &&
              data.address.city &&
              data.address.city.length >= 2 &&
              data.address.city.length <= 100 &&
              data.address.zipCode &&
              this.zipRegex.test(data.address.zipCode) &&
              data.addressLatitude != null &&
              data.addressLongitude != null
            );
        const contactValid = !!(
          data.customerFirstName &&
          data.customerFirstName.length >= 2 &&
          data.customerFirstName.length <= 50 &&
          data.customerLastName &&
          data.customerLastName.length >= 2 &&
          data.customerLastName.length <= 50 &&
          data.customerEmail &&
          this.emailRegex.test(data.customerEmail) &&
          data.customerEmail.length <= 50
        );
        // Block Next when the city-serviced check explicitly rejected.
        // 'pending' / 'error' / 'idle' all pass through — backend
        // re-validates on submit; we just don't want to block on a
        // network failure or a check that hasn't fired yet.
        const cityOk = this.cityServiced() !== 'rejected';
        return addressValid && contactValid && phoneValid && cityOk;
      }
      case 2:
        return !!data.cleaningDate;
      case 3:
        return true;
      default:
        return true;
    }
  }

  saveCurrentAddressAsSaved(label: string): Promise<boolean> {
    return this.savedAddress.saveCurrentAddressAsSaved(label);
  }

  async submitOrder(saveAddress?: { label: string } | null): Promise<void> {
    const data = this.formData();
    if (!data.cleaningDate) return;

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
    // Backend validator is XOR: send savedAddressId OR customerAddress, never both.
    const command = new CreateOrderCommand({
      customerName: `${data.customerFirstName} ${data.customerLastName}`.trim(),
      customerEmail: data.customerEmail,
      customerPhone: data.customerPhone,
      customerAddress: savedId ? undefined : new CustomerAddress(data.address),
      savedAddressId: savedId ?? undefined,
      selectedServiceIds: data.selectedServiceIds,
      selectedPackageIds: data.selectedPackageIds,
      rooms: data.rooms,
      bathrooms: data.bathrooms,
      extras: data.extras,
      cleaningDate: cleaningDate,
      paymentType: data.paymentType,
      currencyId: quoted.currencyId,
      // Send the server-quoted total unchanged — it already includes any
      // express surcharge for the quoted slot. `CreateOrder.PriceMatchesAsync`
      // validates against the same calculator result (`result.TotalPrice ==
      // command.TotalPrice`), exact decimal match, so any client-side price
      // math here would be rejected.
      totalPrice: quoted.totalPrice,
      language: this.translate.currentLang || this.translate.getDefaultLang(),
      promoCode: promoCodeToSend,
      // Referral is a signup-only benefit now — the checkout wizard never
      // populates it. Backend still accepts the field for other callers.
      referralCode: undefined,
      // Future Cleansia Plus perk — customer-requested cleaner. Web wizard
      // doesn't surface this picker yet (waiting on the Plus rollout); send
      // undefined so the backend skips the matching boost. The field is
      // `required` in the NSwag-generated interface but accepts undefined.
      preferredEmployeeId: undefined,
    });

    if (data.paymentType === PaymentType.Card) {
      this.customerClient.paymentClient
        .createOrder(command)
        .pipe(
          takeUntil(this.destroyed$),
          catchError(() => of(null)),
          finalize(() => this.submitting.set(false)),
        )
        .subscribe((response) => {
          if (!response) {
            this.snackbarService.showError(
              this.translate.instant('pages.order.submit_error'),
            );
            return;
          }
          if (response.id) {
            this.guestOrderService.save(response.id, data.customerEmail);
          }
          if (response.stripeSessionId) {
            if (this.isBrowser) window.location.href = response.stripeSessionId;
          } else {
            this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS], {
              queryParams: { type: 'card' },
            });
          }
        });
    } else {
      this.customerClient.orderClient
        .createOrder(command)
        .pipe(
          takeUntil(this.destroyed$),
          catchError(() => of(null)),
          finalize(() => this.submitting.set(false)),
        )
        .subscribe((response) => {
          if (!response) {
            this.snackbarService.showError(
              this.translate.instant('pages.order.submit_error'),
            );
            return;
          }
          if (response.id) {
            this.guestOrderService.save(response.id, data.customerEmail);
          }
          this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS], {
            queryParams: { type: 'cash' },
          });
        });
    }
  }
}
