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
  ExtraListItem,
  QuoteOrderCommand,
  QuoteOrderResponse,
  ValidatePromoCodeCommand,
  ValidateReferralQuery,
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
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { GuestOrderService } from '@cleansia-customer/orders';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, filter, firstValueFrom, of, switchMap, tap } from 'rxjs';
import {
  EXPRESS_SURCHARGE_RATE,
  ORDER_WIZARD_INITIAL_DATA,
  OrderWizardFormData,
  PromoCodeUiState,
  RebookParams,
  ReferralUiState,
  STANDARD_LEAD_TIME_HOURS,
  EXPRESS_LEAD_TIME_HOURS,
} from './order-wizard.models';

/**
 * Snapshot of the wizard inputs that affect pricing. Sorted arrays so
 * "same set, different insertion order" hashes equal in `distinctUntilChanged`.
 */
interface QuoteInputs {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  // Slugs of catalog extras the user toggled on (sorted for stable
  // distinctUntilChanged hashing). Empty when the user hasn't toggled
  // anything in the extras section.
  selectedExtraSlugs: string[];
  rooms: number;
  bathrooms: number;
  currencyId: string | null;
  // ISO-8601 UTC of the chosen slot — drives the express-surcharge check
  // server-side. Null until the user picks a slot.
  cleaningDate: string | null;
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
  private readonly destroyRef = inject(DestroyRef);

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
   * Service-area client-side check. Backend rejects orders in non-served
   * cities with `city.not_serviced`, but that only fires on submit — the
   * wizard surfaces an inline warning earlier so the user doesn't waste
   * time filling out the rest of the form. Backend stays the source of
   * truth; this is purely UX defense-in-depth.
   *
   *  - 'idle'    → no city yet, nothing to check
   *  - 'pending' → query in flight
   *  - 'ok'      → city matches a ServiceCity row
   *  - 'rejected'→ city not served (show banner + disable Next)
   *  - 'error'   → network failed; treat as pass-through, backend re-checks
   */
  readonly cityServiced = signal<'idle' | 'pending' | 'ok' | 'rejected' | 'error'>('idle');
  /** Internal cache: avoids re-querying when city/country haven't changed. */
  private lastCityCheckKey = '';

  /**
   * Server-quoted base total — does NOT include the express surcharge. Use
   * [displayedTotalPrice] for what the user sees in the summary.
   */
  totalPrice = computed(() => this.quote()?.totalPrice ?? 0);

  /**
   * True when the currently-selected (date, time) falls inside the 2–4h lead
   * window. The backend's QuoteOrder endpoint does NOT know about the cleaning
   * date/time, so the express surcharge is applied client-side here. Backend
   * mirrors the same `EXPRESS_SURCHARGE_RATE = 0.20` in `BookingPolicy.cs`
   * and grosses up at CreateOrder time — keep those two constants in sync.
   */
  readonly isExpressSlot = computed(() => {
    const data = this.formData();
    if (!data.cleaningDate || !data.cleaningTime) return false;
    const [h, m] = data.cleaningTime.split(':').map(Number);
    if (Number.isNaN(h) || Number.isNaN(m)) return false;
    const slot = new Date(data.cleaningDate);
    slot.setHours(h, m, 0, 0);
    const hoursAhead = (slot.getTime() - Date.now()) / (1000 * 60 * 60);
    return hoursAhead >= EXPRESS_LEAD_TIME_HOURS && hoursAhead < STANDARD_LEAD_TIME_HOURS;
  });

  /**
   * 20% surcharge amount (CZK), or 0 when the slot isn't express. Computed
   * against the POST-discount subtotal so the surcharge tracks what the user
   * actually pays. Mirrors backend CreateOrder.Handler order: discount on raw,
   * then surcharge on the discounted price.
   */
  readonly expressSurcharge = computed(() => {
    if (!this.isExpressSlot()) return 0;
    const discounted = Math.max(0, this.totalPrice() - this.effectiveDiscount());
    return discounted * EXPRESS_SURCHARGE_RATE;
  });

  /**
   * Final price the user pays — raw subtotal minus best-of-three discount,
   * plus express surcharge on the discounted total. Sidebar + summary both
   * render this so they always agree.
   */
  readonly displayedTotalPrice = computed(() => {
    const discounted = Math.max(0, this.totalPrice() - this.effectiveDiscount());
    return discounted + this.expressSurcharge();
  });

  /** Inputs that affect the server quote. Sorted ids so the snapshot is stable. */
  private readonly quoteInputs = computed<QuoteInputs>(() => {
    const data = this.formData();
    // extras is a slug → boolean map; pull just the slugs that are `true`
    // and sort them so the snapshot is stable under reorder.
    const selectedExtraSlugs = Object.entries(data.extras)
      .filter(([, on]) => on)
      .map(([slug]) => slug)
      .sort();
    // Compose the actual slot moment for the quote. `data.cleaningDate` is
    // the day-only Date the user picked from the calendar; `data.cleaningTime`
    // is the local-clock hour ("14:00"). Building the slot the same way
    // submit() does keeps the backend's express-surcharge check consistent
    // between /Quote and /Create — otherwise the quote sees midnight (no
    // surcharge) but Create sees the real slot (surcharge applies) and the
    // PriceMatchesAsync validator rejects with order.total_price.not_match.
    let cleaningDateIso: string | null = null;
    if (data.cleaningDate && data.cleaningTime) {
      const [h, m] = data.cleaningTime.split(':').map(Number);
      if (!Number.isNaN(h) && !Number.isNaN(m)) {
        const slot = new Date(
          data.cleaningDate.getFullYear(),
          data.cleaningDate.getMonth(),
          data.cleaningDate.getDate(),
          h,
          m,
          0,
          0,
        );
        cleaningDateIso = slot.toISOString();
      }
    }
    return {
      selectedServiceIds: [...data.selectedServiceIds].sort(),
      selectedPackageIds: [...data.selectedPackageIds].sort(),
      selectedExtraSlugs,
      rooms: data.rooms,
      bathrooms: data.bathrooms,
      currencyId: null, // backend default until a currency picker ships
      // Backend computes the surcharge from this; null skips the surcharge
      // check (initial Step 1 quote before the user has chosen a slot).
      cleaningDate: cleaningDateIso,
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

    toObservable(this.quoteInputs)
      .pipe(
        // 800ms matches the typing-pause UX norm for booking wizards and
        // keeps us well under the backend's "interactive" rate-limit
        // (60/min) even if the user rapidly steps rooms / extras. The
        // earlier 400ms was tuned for an unthrottled endpoint.
        debounceTime(800),
        // Cheap structural compare via the same JSON shape we use to
        // detect stale inputs at submit time — same comparator the
        // imperative refreshQuoteNow path uses, so the two stay in sync.
        distinctUntilChanged((a, b) => this.quoteInputsEqual(a, b)),
        tap((inputs) => {
          // Empty selections — clear the quote immediately. No HTTP needed.
          if (this.isEmptyInputs(inputs)) {
            this.quote.set(null);
            this.lastQuotedInputs.set(null);
            this.quoting.set(false);
          }
        }),
        filter((inputs) => !this.isEmptyInputs(inputs)),
        // Skip when the most recent successful quote already matches the
        // pending inputs — the form likely changed and reverted within
        // the debounce window, and re-issuing the same request would
        // just burn a rate-limit token for an identical answer.
        filter((inputs) => !this.quoteInputsEqual(inputs, this.lastQuotedInputs())),
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
                selectedExtraSlugs: inputs.selectedExtraSlugs,
                cleaningDate: inputs.cleaningDate ? new Date(inputs.cleaningDate) : undefined,
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
        takeUntilDestroyed(this.destroyRef),
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
            selectedExtraSlugs: inputs.selectedExtraSlugs,
            cleaningDate: inputs.cleaningDate ? new Date(inputs.cleaningDate) : undefined,
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

  /**
   * Promo discount the user just applied via the dialog (client-side validation).
   */
  effectivePromoDiscount = computed(() => {
    const state = this.promoCodeState();
    return state.kind === 'valid' ? state.discount : 0;
  });

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
    // Validate against the price the user is actually charged — backend's
    // CreateOrder.Handler resolves promo discounts against `finalTotalPrice`
    // (post-express-surcharge), so a bare-subtotal validation could fail a
    // min-order threshold that would otherwise pass on the real charge.
    const subtotal = this.displayedTotalPrice() ?? 0;
    try {
      const resp = await firstValueFrom(
        this.customerClient.promoCodeClient.validate(
          new ValidatePromoCodeCommand({
            code: normalized,
            orderSubtotal: subtotal,
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
          new ValidateReferralQuery({
            code: normalized,
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
    // `getServiced` returns only countries the company operates in. The old
    // `getOverview` call alphabetically returned the full catalog, so the
    // auto-select-first-country fallback silently picked Argentina for
    // every CZ booking — the address persisted with CountryId=Argentina and
    // the backend now (rightly) rejects that. Service-area work in
    // planning/active/service-areas.md.
    this.customerClient.countryClient.getServiced().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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
    this.customerClient.extraClient.getOverview().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (extras) => this.extras.set([...extras].sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0))),
      error: () => this.extras.set([]),
    });

    const loggedIn = this.authService.isLoggedIn();
    this.isAuthenticated.set(loggedIn);

    if (loggedIn) {
      if (!this.savedAddressStore.loaded()) {
        this.savedAddressStore.refresh();
      }
      this.customerClient.userClient.getCurrent().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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
    // City / country can change via multiple paths (Mapbox pick, manual
    // edit, saved-address selection). Centralise the service-area check
    // here so every path triggers it uniformly.
    if (partial.address !== undefined) {
      this.refreshCityServicedCheck();
    }
  }

  /**
   * Fire-and-forget service-area lookup. Skips when nothing's changed
   * (avoids hammering /api/ServiceCity on every keystroke), skips during
   * SSR, and degrades to 'error' (pass-through) on network failure so a
   * flaky connection can't strand the user — backend re-validates on
   * submit anyway.
   */
  private refreshCityServicedCheck(): void {
    if (!this.isBrowser) return;
    const addr = this.formData().address;
    const countryId = addr.countryId ?? '';
    const city = (addr.city ?? '').trim();
    if (!countryId || !city) {
      this.cityServiced.set('idle');
      this.lastCityCheckKey = '';
      return;
    }
    const key = `${countryId}|${city.toLowerCase()}`;
    if (key === this.lastCityCheckKey) return;
    this.lastCityCheckKey = key;
    this.cityServiced.set('pending');
    this.customerClient.apiClient
      .serviceCity(countryId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (cities) => {
          // Stale-response guard: if the user already typed past this
          // city between the request and the response, drop the result.
          if (key !== this.lastCityCheckKey) return;
          const normalized = city.toLowerCase();
          const match = (cities ?? []).some(
            (c) => (c.name ?? '').trim().toLowerCase() === normalized
          );
          this.cityServiced.set(match ? 'ok' : 'rejected');
        },
        error: () => {
          if (key !== this.lastCityCheckKey) return;
          this.cityServiced.set('error');
        },
      });
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
      // Send the bare server-quoted total (no client-applied surcharge).
      // `CreateOrder.PriceMatchesAsync` validates against the same calculator
      // result (`result.TotalPrice == command.TotalPrice`) — exact decimal
      // match. The handler then grosses up itself when the slot is express.
      // We avoid sending a JS-multiplied surcharged value because Number×1.2
      // drifts on prices like 1234.56 (→ 1481.4720000000002), which would
      // fail the strict-equality grossed-up branch of the validator.
      // Display-side, the user already sees the surcharge in `displayedTotalPrice`.
      totalPrice: quoted.totalPrice,
      language: this.translate.currentLang || this.translate.getDefaultLang(),
      promoCode: promoCodeToSend,
      referralCode: referralCodeToSend,
      // Future Cleansia Plus perk — customer-requested cleaner. Web wizard
      // doesn't surface this picker yet (waiting on the Plus rollout); send
      // undefined so the backend skips the matching boost. The field is
      // `required` in the NSwag-generated interface but accepts undefined.
      preferredEmployeeId: undefined,
    });

    if (data.paymentType === PaymentType.Card) {
      this.customerClient.paymentClient.createOrder(command).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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
      this.customerClient.orderClient.createOrder(command).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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
