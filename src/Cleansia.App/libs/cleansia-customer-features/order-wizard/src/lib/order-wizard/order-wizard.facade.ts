import { DestroyRef, inject, Injectable, PLATFORM_ID, signal, computed } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import {
  AddSavedAddressCommand,
  AddressDto,
  CreateOrderCommand,
  CustomerAddress,
  CustomerAuthService,
  CustomerClient,
  QuoteOrderCommand,
  QuoteOrderResponse,
  ValidatePromoCodeCommand,
  ValidateReferralCommand,
} from '@cleansia/customer-services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import {
  CategoryDto,
  CountryListItem,
  PackageListItem,
  PaymentType,
  ServiceListItem,
} from '@cleansia/partner-services';
import { CleansiaCustomerRoute, GuestOrderService, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, filter, firstValueFrom, of, switchMap, tap } from 'rxjs';
import {
  ORDER_WIZARD_INITIAL_DATA,
  OrderWizardFormData,
  PromoCodeUiState,
  RebookParams,
  ReferralUiState,
} from './order-wizard.models';

/**
 * Snapshot of the wizard inputs that affect pricing. Sorted arrays so
 * "same set, different insertion order" hashes equal in `distinctUntilChanged`.
 */
interface QuoteInputs {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  rooms: number;
  bathrooms: number;
  currencyId: string | null;
}

@Injectable()
export class OrderWizardFacade {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  isAuthenticated = signal(false);

  services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });
  packages = toSignal(this.store.select(selectCustomerPackages), {
    initialValue: [] as PackageListItem[],
  });
  countries = signal<CountryListItem[]>([]);
  readonly savedAddresses = this.savedAddressStore.addresses;
  readonly selectedSavedAddressId = signal<string | null>(null);

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
  // Mirrors the mobile pattern: debounced /api/Order/Quote on every
  // selection change. The server is the single source of truth for the
  // total — clients never compute prices themselves. Reasons:
  //  - Express surcharge, loyalty discount, future extras pricing all
  //    live server-side; client formulas would drift the moment they
  //    change.
  //  - Backend's `PriceMatchesAsync` validator on /Create rejects orders
  //    whose totalPrice doesn't match the server calculation. Live quote
  //    guarantees we always submit the authoritative number.
  readonly quote = signal<QuoteOrderResponse | null>(null);
  readonly quoting = signal(false);
  /** Snapshot of inputs that produced the current `quote()`, for cache reuse. */
  private readonly lastQuotedInputs = signal<QuoteInputs | null>(null);

  /**
   * Display total — server quote when available, else 0. Templates that
   * previously read `totalPrice()` should keep working without code changes;
   * downstream code that needs the actual quote object should read `quote()`.
   */
  totalPrice = computed(() => this.quote()?.totalPrice ?? 0);

  /** Inputs that affect the server quote. Sorted ids so the snapshot is stable. */
  private readonly quoteInputs = computed<QuoteInputs>(() => {
    const data = this.formData();
    return {
      selectedServiceIds: [...data.selectedServiceIds].sort(),
      selectedPackageIds: [...data.selectedPackageIds].sort(),
      rooms: data.rooms,
      bathrooms: data.bathrooms,
      currencyId: null, // backend default until a currency picker ships
    };
  });

  private isEmptyInputs(i: QuoteInputs): boolean {
    return i.selectedServiceIds.length === 0 && i.selectedPackageIds.length === 0;
  }

  private quoteInputsEqual(a: QuoteInputs | null, b: QuoteInputs | null): boolean {
    if (a === b) return true;
    if (!a || !b) return false;
    return JSON.stringify(a) === JSON.stringify(b);
  }

  constructor() {
    // SSR guard: don't fire HTTP during server render. The signal stays null
    // on the server and the client picks up the debounce loop after hydrate.
    if (!this.isBrowser) return;

    const destroyRef = inject(DestroyRef);
    toObservable(this.quoteInputs)
      .pipe(
        debounceTime(400),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        tap((inputs) => {
          // Empty selections — clear the quote immediately. No HTTP needed.
          if (this.isEmptyInputs(inputs)) {
            this.quote.set(null);
            this.lastQuotedInputs.set(null);
            this.quoting.set(false);
          }
        }),
        filter((inputs) => !this.isEmptyInputs(inputs)),
        tap(() => this.quoting.set(true)),
        switchMap((inputs) =>
          this.customerClient.orderClient
            .quote(
              new QuoteOrderCommand({
                selectedServiceIds: inputs.selectedServiceIds,
                selectedPackageIds: inputs.selectedPackageIds,
                rooms: inputs.rooms,
                bathrooms: inputs.bathrooms,
                currencyId: inputs.currencyId ?? undefined,
              }),
            )
            .pipe(
              // Silent during editing — keep the prior quote so the user
              // doesn't see a "—" flash on a transient backend hiccup. Errors
              // surface only at submit time.
              catchError(() => of(null)),
              tap((resp) => {
                if (resp) {
                  this.quote.set(resp);
                  this.lastQuotedInputs.set(inputs);
                }
              }),
            ),
        ),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe(() => this.quoting.set(false));
  }

  /**
   * Imperative quote refresh. Used by submit when the cached quote's
   * inputs don't match the current wizard state — we await a fresh
   * /Quote call before /Create so the backend validator can't reject
   * us for a stale total.
   */
  async refreshQuoteNow(): Promise<QuoteOrderResponse | null> {
    const inputs = this.quoteInputs();
    if (this.isEmptyInputs(inputs)) {
      this.quote.set(null);
      this.lastQuotedInputs.set(null);
      return null;
    }
    this.quoting.set(true);
    try {
      const resp = await firstValueFrom(
        this.customerClient.orderClient.quote(
          new QuoteOrderCommand({
            selectedServiceIds: inputs.selectedServiceIds,
            selectedPackageIds: inputs.selectedPackageIds,
            rooms: inputs.rooms,
            bathrooms: inputs.bathrooms,
            currencyId: inputs.currencyId ?? undefined,
          }),
        ),
      );
      this.quote.set(resp);
      this.lastQuotedInputs.set(inputs);
      return resp;
    } catch {
      return null;
    } finally {
      this.quoting.set(false);
    }
  }

  /** True when the current input snapshot matches the inputs that produced `quote()`. */
  private cachedQuoteMatchesCurrentState(): boolean {
    return !!this.quote() && this.quoteInputsEqual(this.quoteInputs(), this.lastQuotedInputs());
  }

  // ─── Promo code validation ───────────────────────────────────
  //
  // Wolt-style: the summary step shows a tappable row that opens a modal. The
  // dialog calls `validatePromoCodeNow(code)` exactly once on Apply. No
  // debounced auto-validation pipeline anymore — that produced too much noise
  // and a chatty backend. The state machine still lives here so the row can
  // render the applied chip and the dialog can read live state.
  promoCode = signal('');
  promoCodeState = signal<PromoCodeUiState>({ kind: 'idle' });

  // ─── Referral code (late-acceptance path) ───────────────────
  //
  // Same row+dialog pattern as promo. Backend treats invalid codes as best-
  // effort (logged, never blocks submit), so the dialog only surfaces
  // validation feedback for clarity — the order will still go through with
  // an unverified referral code if the user types one and skips Apply.
  referralCode = signal('');
  referralState = signal<ReferralUiState>({ kind: 'idle' });

  /**
   * Effective discount preview for the summary line. Returns 0 unless the
   * promo state is `valid`. When tier discounts ship client-side later, fold
   * `max(tier, promo)` here per the loyalty Phase B spec ("best-wins").
   */
  effectivePromoDiscount = computed(() => {
    const state = this.promoCodeState();
    return state.kind === 'valid' ? state.discount : 0;
  });

  setPromoCode(value: string): void {
    this.promoCode.set(value);
    this.updateFormData({ promoCode: value });
  }

  setReferralCode(value: string): void {
    this.referralCode.set(value);
    this.updateFormData({ referralCode: value });
  }

  /**
   * Apply-button handler from the promo dialog. Single backend call, no
   * debounce. Empty input resets to idle without touching the network.
   * Returns the resolved state so the dialog can react to it.
   */
  async validatePromoCodeNow(code: string): Promise<PromoCodeUiState> {
    const normalized = code.trim().toUpperCase();
    if (!normalized) {
      this.promoCodeState.set({ kind: 'idle' });
      this.setPromoCode('');
      return { kind: 'idle' };
    }
    this.promoCodeState.set({ kind: 'validating' });
    const subtotal = this.totalPrice() ?? 0;
    try {
      const resp = await firstValueFrom(
        this.customerClient.promoCodeClient.validate(
          new ValidatePromoCodeCommand({
            code: normalized,
            orderSubtotal: subtotal,
            userId: undefined,
          }),
        ),
      );
      const newState: PromoCodeUiState =
        resp.isValid && resp.discountAmount != null
          ? { kind: 'valid', discount: resp.discountAmount }
          : { kind: 'invalid', error: resp.errorCode ?? null };
      this.promoCodeState.set(newState);
      if (newState.kind === 'valid') {
        this.setPromoCode(normalized);
      }
      return newState;
    } catch {
      const newState: PromoCodeUiState = { kind: 'invalid', error: null };
      this.promoCodeState.set(newState);
      return newState;
    }
  }

  /**
   * Apply-button handler from the referral dialog. Mirrors `validatePromoCodeNow`.
   * Backend doesn't fail orders on bad referral codes, so this is purely UX
   * confirmation — but we still gate the row's "applied" chip on a `valid`
   * state so the user knows it stuck.
   */
  async validateReferralCodeNow(code: string): Promise<ReferralUiState> {
    const normalized = code.trim().toUpperCase();
    if (!normalized) {
      this.referralState.set({ kind: 'idle' });
      this.setReferralCode('');
      return { kind: 'idle' };
    }
    this.referralState.set({ kind: 'validating' });
    try {
      const resp = await firstValueFrom(
        this.customerClient.referralClient.validate(
          new ValidateReferralCommand({
            code: normalized,
            acceptingUserId: undefined,
          }),
        ),
      );
      const newState: ReferralUiState = resp.isValid
        ? { kind: 'valid', referrerFirstName: resp.referrerFirstName ?? null }
        : { kind: 'invalid', error: resp.errorCode ?? null };
      this.referralState.set(newState);
      if (newState.kind === 'valid') {
        this.setReferralCode(normalized);
      }
      return newState;
    } catch {
      const newState: ReferralUiState = { kind: 'invalid', error: null };
      this.referralState.set(newState);
      return newState;
    }
  }

  /** Wipes the applied promo state — used by the row's clear-X button. */
  clearPromoCode(): void {
    this.setPromoCode('');
    this.promoCodeState.set({ kind: 'idle' });
  }

  /** Wipes the applied referral state — used by the row's clear-X button. */
  clearReferralCode(): void {
    this.setReferralCode('');
    this.referralState.set({ kind: 'idle' });
  }

  initialize(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
    this.customerClient.countryClient.getOverview().subscribe({
      next: (countries) => {
        this.countries.set(countries);
        // Auto-select first country if address has no country set
        if (countries.length > 0 && !this.formData().address.countryId) {
          this.updateFormData({
            address: new AddressDto({
              ...this.formData().address,
              countryId: countries[0].id ?? '',
            }),
          });
        }
      },
    });

    const loggedIn = this.authService.isLoggedIn();
    this.isAuthenticated.set(loggedIn);

    if (loggedIn) {
      if (!this.savedAddressStore.loaded()) {
        this.savedAddressStore.refresh();
      }
      this.customerClient.userClient.getCurrent().subscribe({
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
    const addr = this.savedAddresses().find((a) => a.id === addressId);
    if (!addr) return;
    this.selectedSavedAddressId.set(addressId);
    this.updateFormData({
      address: new AddressDto({
        street: addr.street ?? '',
        city: addr.city ?? '',
        zipCode: addr.zipCode ?? '',
        countryId: addr.countryId ?? '',
        state: addr.state ?? '',
      }),
      addressLatitude: addr.latitude ?? null,
      addressLongitude: addr.longitude ?? null,
    });
  }

  updateFormData(partial: Partial<OrderWizardFormData>): void {
    this.formData.update((current) => ({ ...current, ...partial }));
  }

  updateAddressFromForm(next: AddressDto): void {
    // Manual edits to the address break the saved-address binding — the user is
    // entering a one-off. Nulling the id ensures we POST customerAddress instead
    // of savedAddressId on submit. Also clears any previously-picked coords —
    // they don't belong to this freshly-typed address anymore.
    this.selectedSavedAddressId.set(null);
    this.updateFormData({
      address: next,
      addressLatitude: null,
      addressLongitude: null,
    });
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
    this.selectedSavedAddressId.set(null);
    const current = this.formData().address;
    this.updateFormData({
      address: new AddressDto({
        street: suggestion.street || current.street || '',
        city: suggestion.city || current.city || '',
        zipCode: suggestion.zipCode || current.zipCode || '',
        countryId: current.countryId ?? '',
        state: current.state ?? '',
      }),
      addressLatitude: suggestion.latitude,
      addressLongitude: suggestion.longitude,
    });
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
    return this.selectedSavedAddressId() !== null;
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
        return addressValid && contactValid && phoneValid;
      }
      case 2:
        return !!data.cleaningDate;
      case 3:
        return true;
      default:
        return true;
    }
  }

  async saveCurrentAddressAsSaved(label: string): Promise<boolean> {
    const data = this.formData();
    const addr = data.address;
    // Backend requires Mapbox-resolved coordinates. If the user hasn't picked
    // a suggestion, we can't save — bail out (TASK-005 will surface a hint UI).
    const lat = data.addressLatitude;
    const lng = data.addressLongitude;
    if (lat === undefined || lng === undefined || lat === null || lng === null) {
      return false;
    }
    const command = new AddSavedAddressCommand({
      label,
      street: addr.street ?? '',
      city: addr.city ?? '',
      zipCode: addr.zipCode ?? '',
      countryId: addr.countryId ?? undefined,
      setAsDefault: false,
      latitude: lat,
      longitude: lng,
      userId: undefined,
    });
    const result = await this.savedAddressStore.add(command);
    if (result?.id) {
      this.selectedSavedAddressId.set(result.id);
      return true;
    }
    return false;
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
    if (!quoted || !this.cachedQuoteMatchesCurrentState()) {
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
    const cleaningDate = new Date(Date.UTC(
      selectedDate.getFullYear(),
      selectedDate.getMonth(),
      selectedDate.getDate(),
      hours, minutes, 0, 0
    ));

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
    // Referral code: forward whenever the user typed something (trimmed).
    // Backend treats invalid codes as no-op and never fails the order, so we
    // don't gate on the live `referralState` like we do for promo. This keeps
    // the late-acceptance flow forgiving — a typo'd code on submit just
    // results in no referral being recorded.
    const trimmedReferral = this.referralCode().trim();
    const referralCodeToSend = trimmedReferral
      ? trimmedReferral.toUpperCase()
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
      totalPrice: quoted.totalPrice,
      language: this.translate.currentLang || this.translate.getDefaultLang(),
      // Backend enriches from JWT NameIdentifier when authenticated; guest
      // checkout sends empty and the handler skips ownership checks.
      userId: undefined,
      promoCode: promoCodeToSend,
      referralCode: referralCodeToSend,
      // Future Cleansia Plus perk — customer-requested cleaner. Web wizard
      // doesn't surface this picker yet (waiting on the Plus rollout); send
      // undefined so the backend skips the matching boost. The field is
      // `required` in the NSwag-generated interface but accepts undefined.
      preferredEmployeeId: undefined,
    });

    if (data.paymentType === PaymentType.Card) {
      this.customerClient.paymentClient.createOrder(command).subscribe({
        next: (response) => {
          this.submitting.set(false);
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
        },
        error: () => {
          this.submitting.set(false);
          this.snackbarService.showError(
            this.translate.instant('pages.order.submit_error')
          );
        },
      });
    } else {
      this.customerClient.orderClient.createOrder(command).subscribe({
        next: (response) => {
          this.submitting.set(false);
          if (response.id) {
            this.guestOrderService.save(response.id, data.customerEmail);
          }
          this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS], {
              queryParams: { type: 'cash' },
            });
        },
        error: () => {
          this.submitting.set(false);
          this.snackbarService.showError(
            this.translate.instant('pages.order.submit_error')
          );
        },
      });
    }
  }
}
