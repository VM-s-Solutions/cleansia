import { isPlatformBrowser } from '@angular/common';
import { computed, effect, inject, Injectable, Injector, PLATFORM_ID, signal, untracked } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddSavedAddressCommand,
  CardCaptureFacade,
  chosenPackagesByService,
  CreateRecurringBookingCommand,
  CustomerClient,
  DeleteRecurringBookingCommand,
  DirtinessLevel,
  GetMyServingCleanersResponse,
  includedServicesAlreadyChosen,
  MembershipStatus,
  PackageListItem,
  PaymentType,
  PreferredCleanerOption,
  QuoteOrderCommand,
  QuoteOrderResponse,
  RecurringBookingTemplateDto,
  ServiceListItem,
  SetRecurringBookingActiveCommand,
  toPreferredCleanerOptions,
  UpdateRecurringBookingCommand,
} from '@cleansia/customer-services';
import { CashEligibility, cashIsRefused, resolveCashEligibility } from '@cleansia/models';
import {
  CleansiaCustomerRoute,
  DialogService,
  extractApiErrorCode,
  SnackbarService,
} from '@cleansia/services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerPackagesCatalogue,
  selectCustomerServices,
  selectCustomerServicesCatalogue,
  selectMarketCountryId,
} from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom, takeUntil } from 'rxjs';
import {
  PricedSelection,
  RecurringPrefillParams,
  RecurringWizardFormData,
  RECURRING_WIZARD_INITIAL_DATA,
  canAdvance,
  canSubmit,
  missingFields,
  nextOccurrenceUtc,
  samePricedSelection,
  scheduleCashReason,
} from './recurring-bookings.models';

/** A server-quoted figure with the currency the server priced it in. */
export interface QuotedPrice {
  amount: number;
  currency: string | null;
}

const ONE_DAY_MS = 24 * 60 * 60 * 1000;

const PARKED_FORM_STORAGE_KEY = 'cleansia_recurring_form_parked';

/** Cash refusals the customer answers on this form by paying by card. */
const CASH_REFUSALS: readonly string[] = [
  'order.cash_not_available',
  'order.cash_unpaid_receivable',
  'order.cash_open_bookings_limit_reached',
];

interface ParkedForm {
  templateId: string | null;
  data: RecurringWizardFormData;
}

function priceOf(quoted: QuoteOrderResponse): QuotedPrice {
  return {
    amount: quoted.finalPriceAfterDiscount ?? quoted.totalPrice,
    // The quote names its currency; a blank one renders the bare number, never a guessed unit.
    currency: quoted.currencyCode ?? '',
  };
}

/**
 * Single facade for both the recurring-bookings list view and the create
 * wizard. Signal-only state — matches the order-wizard convention; no NgRx
 * slice unless cross-screen caching becomes valuable.
 *
 * Lifetime: each screen provides its own instance (the list and the wizard
 * both declare it in `providers`), so the templates cache is per screen and
 * "Create" → submit → back-to-list re-fetches; the shared catalogue and
 * addresses live in the stores, not here.
 */
@Injectable()
export class RecurringBookingsFacade extends UnsubscribeControlDirective {
  // Always go through CustomerClient — injecting RecurringBookingClient
  // directly hits NSwag's empty-string default baseUrl, sending requests
  // back to the SPA's own origin instead of the configured API URL.
  private readonly customerClient = inject(CustomerClient);
  private readonly client = this.customerClient.recurringBookingClient;
  // The platform's own price authority. A schedule card states a figure the
  // customer plans around, so it is QUOTED rather than recomputed here — the
  // discount stack (membership, loyalty tier, promo) lives behind this endpoint
  // and a second implementation of it would disagree the first time one moved.
  private readonly orderClient = this.customerClient.orderClient;
  private readonly membershipClient = this.customerClient.membershipClient;
  private readonly snackbar = inject(SnackbarService);
  private readonly dialog = inject(DialogService);
  private readonly translate = inject(TranslateService);
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly store = inject(Store);
  private readonly injector = inject(Injector);
  private readonly cardCapture = inject(CardCaptureFacade);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // ─── List state ────────────────────────────────────────────────────
  readonly templates = signal<RecurringBookingTemplateDto[]>([]);
  readonly listLoading = signal(false);
  readonly listLoaded = signal(false);
  /** Id of the template currently being mutated (pause/resume/delete), or null. */
  readonly mutatingId = signal<string | null>(null);

  // ─── The entitlement ───────────────────────────────────────────────
  // `CreateRecurringBooking` refuses a caller without Plus
  // (`RecurringTemplateMembershipRequired`), so the honest screen for a
  // non-member is the paywall, not an empty list with a button that 400s.
  // Read here rather than through the profile lib's MembershipFacade: that
  // library already lazy-imports THIS one for its `recurring/*` child routes,
  // and importing it back would close the cycle.
  readonly isMember = signal(false);
  readonly membershipLoaded = signal(false);

  // ─── Edit ──────────────────────────────────────────────────────────
  /** Template being edited, or null when the form is creating a new one. */
  readonly editingId = signal<string | null>(null);
  /**
   * The server does not accept the schedule's preferred cleaner (the address may be in a market they
   * are not paid in). The preference is kept until the customer chooses to save without it.
   */
  readonly preferredCleanerRefused = signal(false);
  /** The server refuses a start on or after the schedule's end date, which this form cannot edit. */
  readonly latestStartsOn = computed(() => {
    const endsOn = this.formData().endsOn;
    return endsOn ? new Date(endsOn.getTime() - ONE_DAY_MS) : null;
  });

  // ─── Prices ────────────────────────────────────────────────────────
  /** templateId → quoted price per clean. Absent until the quote lands. */
  readonly templatePrices = signal<Record<string, QuotedPrice>>({});
  /** The price of whatever the form currently describes. */
  readonly formPrice = signal<QuotedPrice | null>(null);
  readonly quoting = signal(false);
  /** The crew the server last quoted, with the selection it was quoted for. */
  private readonly formCrew = signal<{ selection: PricedSelection; requiredEmployees: number } | null>(
    null,
  );
  private formQuoteSequence = 0;
  /** The crew for the form as it is now; null until a quote for this very selection lands. */
  private readonly formRequiredEmployees = computed(() => {
    const crew = this.formCrew();
    return crew && samePricedSelection(crew.selection, this.pricedSelection())
      ? crew.requiredEmployees
      : null;
  });

  // A schedule is always an account's, so only the crew decides.
  readonly cashEligibility = computed<CashEligibility>(() =>
    resolveCashEligibility(true, this.formRequiredEmployees()),
  );
  readonly cashSelectable = computed(() => this.cashEligibility().kind === 'available');
  readonly cashReason = computed(() => scheduleCashReason(this.cashEligibility()));
  /** A cash choice was taken away because it stopped being allowed; cleared by the next choice. */
  readonly cashCleared = signal(false);
  /** Said only while cash is still not available; a selection that allows it again needs no warning. */
  readonly cashClearedNotice = computed(() => this.cashCleared() && !this.cashSelectable());
  private readonly cashEffect = effect(() => {
    if (this.formData().paymentType === PaymentType.Cash && cashIsRefused(this.cashEligibility())) {
      untracked(() => this.dropCash(true));
    }
  });

  // ─── The favourite cleaner (Plus) ──────────────────────────────────
  readonly servingCleaners = signal<GetMyServingCleanersResponse[]>([]);
  readonly servingCleanersLoading = signal(false);
  readonly preferredCleanerOptions = computed<PreferredCleanerOption[]>(() =>
    toPreferredCleanerOptions(
      this.servingCleaners(),
      this.translate.instant('preferred_cleaner.unavailable'),
    ),
  );
  readonly preferredCleanerVisible = computed(() => this.servingCleaners().length > 0);

  /** Drives the address field's own spinner — see `ensureAddresses`. */
  readonly addressesLoading = signal(false);

  readonly cardCaptureVisible = this.cardCapture.visible;
  readonly cardCaptureConsent = this.cardCapture.consentAccepted;
  readonly cardCaptureStarting = this.cardCapture.starting;

  // ─── Wizard state ──────────────────────────────────────────────────
  readonly activeStep = signal(1);
  readonly formData = signal<RecurringWizardFormData>({
    ...RECURRING_WIZARD_INITIAL_DATA,
  });
  /** What the form's quote prices; it changes only when the price can. */
  readonly pricedSelection = computed<PricedSelection>(
    () => {
      const d = this.formData();
      return {
        serviceIds: d.selectedServiceIds,
        packageIds: d.selectedPackageIds,
        rooms: d.rooms,
        bathrooms: d.bathrooms,
        dirtinessLevel: d.dirtinessLevel ?? DirtinessLevel.Normal,
        countryId: this.countryOf(d.savedAddressId),
      };
    },
    { equal: samePricedSelection },
  );
  readonly submitting = signal(false);

  // ─── Shared catalog + addresses (reused across both screens) ───────
  readonly services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });
  readonly packages = toSignal(this.store.select(selectCustomerPackages), {
    initialValue: [] as PackageListItem[],
  });
  readonly savedAddresses = this.savedAddressStore.addresses;
  private readonly servicesCatalogue = toSignal(this.store.select(selectCustomerServicesCatalogue), {
    initialValue: { services: [] as ServiceListItem[], countryId: null as string | null },
  });
  private readonly packagesCatalogue = toSignal(this.store.select(selectCustomerPackagesCatalogue), {
    initialValue: { packages: [] as PackageListItem[], countryId: null as string | null },
  });

  /** The chosen market, which prices the form until a saved address names a country. */
  private readonly marketCountryId = toSignal(this.store.select(selectMarketCountryId), {
    initialValue: null as string | null,
  });
  /**
   * The chosen saved address's country, which decides the currency the schedule is priced in —
   * and with it which catalogue entries can be offered at all. Before an address is chosen the
   * chosen market stands in, the same precedence the one-off wizard and both mobile forms apply.
   */
  private readonly addressCountryId = computed<string | null>(() =>
    this.countryOf(this.formData().savedAddressId) ?? this.marketCountryId(),
  );
  /**
   * Whether the lists on screen are the ones priced for the address — the only lists a selection
   * can honestly be checked against. False while the addresses are still loading, since they
   * decide the market.
   */
  private readonly cataloguePricedForAddress = computed(() => {
    const countryId = this.addressCountryId();
    return (
      !this.addressesLoading() &&
      this.servicesCatalogue().countryId === countryId &&
      this.packagesCatalogue().countryId === countryId
    );
  });

  /** An order's selection waiting for the list it can be checked against. */
  private readonly pendingPrefill = signal<RecurringPrefillParams | null>(null);
  private readonly prefillEffect = effect(() => {
    const params = this.pendingPrefill();
    if (!params || !this.cataloguePricedForAddress()) return;
    const needsServices = params.selectedServiceIds.length > 0;
    const needsPackages = params.selectedPackageIds.length > 0;
    if ((needsServices && this.services().length === 0) || (needsPackages && this.packages().length === 0)) {
      return;
    }

    this.pendingPrefill.set(null);
    const missing = this.prefillFromOrder(params);
    if (missing.length > 0) {
      this.snackbar.showSuccess(
        this.translate.instant('recurring_booking.prefill_dropped_items', {
          items: missing.join(', '),
        }),
      );
    }
  });
  /** The country the catalogue was last read for, so a same-country address switch re-reads nothing. */
  private catalogueCountryId: string | null = null;
  private followingAddressCountry = false;

  // ─── Computed derivations for the template ─────────────────────────
  readonly canAdvance = computed(() => canAdvance(this.activeStep(), this.formData()));
  readonly canSubmit = computed(() => canSubmit(this.formData()));

  /**
   * Set the first time the customer presses save. Until then the form says
   * nothing — nobody wants to be told what is missing from a form they have not
   * filled in yet — and after it, every gap is named.
   */
  readonly submitAttempted = signal(false);

  /** Which required fields are still empty, in the order the form asks them. */
  readonly missing = computed(() => missingFields(this.formData(), this.editingId() === null));

  constructor() {
    super();
    this.cardCapture.connect({
      countryId: () => this.countryOf(this.formData().savedAddressId),
      park: () => this.parkForm(),
      returnUrl: () =>
        `/${CleansiaCustomerRoute.MEMBERSHIP}/recurring/${this.editingId() ?? 'create'}`,
    });
  }

  /**
   * Bootstrap: load templates + addresses + catalog. Safe to call on every
   * list-screen entry — internal `loaded` guards skip redundant fetches.
   */
  async initialize(): Promise<void> {
    // SYNCHRONOUSLY, before any await. This used to run at the END of
    // initialize, three network round trips later, so the form spent that whole
    // time with a null `startsOn` — which `canSubmit` reads — and the submit
    // button was dead with nothing on screen explaining why. Touching the date
    // picker "fixed" it, which is exactly what it looked like from outside.
    this.applyDefaultStartDate();

    this.followAddressCountry();

    // First, because it decides which page the customer is even shown. A
    // failure here reads as "not a member": the paywall is the safe wrong
    // answer, an empty list behind a button the API refuses is not.
    await this.refreshMembership();
    if (!this.isMember()) {
      this.listLoaded.set(true);
      return;
    }

    await this.ensureAddresses();
    await this.refreshList();
  }

  /**
   * Load the customer's saved addresses, whoever is asking.
   *
   * Deliberately NOT part of `initialize`'s membership-gated tail. The create
   * form is a route of its own, and `initialize` returns early for a
   * non-member — so a form reached directly had no addresses at all, and its
   * address select was permanently empty with nothing to explain it.
   *
   * It also does not trust another screen to have loaded them. The profile page
   * happens to populate the same store, which made this look fine whenever the
   * customer had been there first and broken whenever they had not.
   */
  async ensureAddresses(): Promise<void> {
    if (this.addressesLoading()) return;
    this.addressesLoading.set(true);
    try {
      if (!this.savedAddressStore.loaded()) {
        await this.savedAddressStore.refresh();
      }
      this.applyDefaultAddress();
    } finally {
      this.addressesLoading.set(false);
    }
  }

  /** One week out, so the field is never blank. Cheap, and needs no network. */
  private applyDefaultStartDate(): void {
    if (this.formData().startsOn) return;
    const nextWeek = new Date();
    nextWeek.setDate(nextWeek.getDate() + 7);
    nextWeek.setHours(0, 0, 0, 0);
    this.updateFormData({ startsOn: nextWeek });
  }

  private applyDefaultAddress(): void {
    if (this.formData().savedAddressId) return;
    const defaultAddr =
      this.savedAddresses().find((a) => a.isDefault) ?? this.savedAddresses()[0];
    if (defaultAddr?.id) {
      this.updateFormData({ savedAddressId: defaultAddr.id });
    }
  }

  /**
   * Save a new address from inside this form and select it.
   *
   * Without this the answer to "I want to clean somewhere else" was: leave the
   * half-filled schedule, go to the profile, add the address, come back and
   * start again. `CountryId` is optional on the command, so the autocomplete's
   * street/city/zip/coordinates plus a label is the whole form.
   */
  async addAddress(label: string, picked: {
    street: string; city: string; zipCode: string; latitude: number; longitude: number;
  }): Promise<boolean> {
    const command = new AddSavedAddressCommand();
    command.label = label;
    command.street = picked.street;
    command.city = picked.city;
    command.zipCode = picked.zipCode;
    command.latitude = picked.latitude;
    command.longitude = picked.longitude;
    command.setAsDefault = this.savedAddresses().length === 0;

    const created = await this.savedAddressStore.add(command);
    if (!created?.id) return false;
    this.updateFormData({ savedAddressId: created.id });
    return true;
  }

  async refreshMembership(): Promise<void> {
    try {
      const me = await firstValueFrom(
        this.membershipClient.getMine().pipe(takeUntil(this.destroyed$)),
      );
      this.isMember.set(me?.status === MembershipStatus.Active);
    } catch {
      this.isMember.set(false);
    } finally {
      this.membershipLoaded.set(true);
    }
  }

  /**
   * Quote one template's price per clean.
   *
   * Fired per card rather than in one batch because there is no batch quote —
   * a customer has a handful of schedules, not a page of them, and the
   * alternative was to state no figure at all. Failures are SILENT: the card
   * simply omits the price, which is the honest thing for a number we could
   * not get.
   */
  async quoteTemplate(template: RecurringBookingTemplateDto): Promise<void> {
    if (!template.id || this.templatePrices()[template.id]) return;
    const quoted = await this.quote(
      template.selectedServiceIds ?? [],
      template.selectedPackageIds ?? [],
      template.rooms,
      template.bathrooms,
      template.dirtinessLevel ?? DirtinessLevel.Normal,
      this.countryOf(template.savedAddressId),
    );
    if (!quoted) return;
    this.templatePrices.update((all) => ({ ...all, [template.id as string]: priceOf(quoted) }));
  }

  /** Re-quote whatever the form currently describes. Only the latest request's answer is kept. */
  async quoteForm(): Promise<void> {
    const sequence = ++this.formQuoteSequence;
    const selection = this.pricedSelection();
    if (selection.serviceIds.length === 0 && selection.packageIds.length === 0) {
      this.formPrice.set(null);
      this.formCrew.set(null);
      this.quoting.set(false);
      return;
    }
    this.quoting.set(true);
    const quoted = await this.quote(
      selection.serviceIds,
      selection.packageIds,
      selection.rooms,
      selection.bathrooms,
      selection.dirtinessLevel,
      selection.countryId,
    );
    if (sequence !== this.formQuoteSequence) return;
    this.formPrice.set(quoted ? priceOf(quoted) : null);
    const requiredEmployees = quoted?.requiredEmployees ?? null;
    this.formCrew.set(requiredEmployees === null ? null : { selection, requiredEmployees });
    this.quoting.set(false);
  }

  /** The country of a saved address, which decides the currency a schedule is priced in. */
  private countryOf(savedAddressId: string | null | undefined): string | null {
    if (!savedAddressId) return null;
    return this.savedAddresses().find((a) => a.id === savedAddressId)?.countryId || null;
  }

  /**
   * The catalogue is priced per market and the server withholds what has no price in the
   * address country's currency, so it is read for the chosen market first and again for every
   * country the chosen saved address names. A selection the new list no longer offers would make
   * the server refuse the quote outright, so it is trimmed to the new list — with a word to the
   * customer — once that list has landed.
   */
  private followAddressCountry(): void {
    if (this.followingAddressCountry) return;
    this.followingAddressCountry = true;

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
        this.keepSelected('selectedServiceIds', new Set(services.map((s) => s.id)));
      });
    this.store
      .select(selectCustomerPackagesCatalogue)
      .pipe(takeUntil(this.destroyed$))
      .subscribe(({ packages, countryId }) => {
        if (!this.pricedForAddress(countryId)) return;
        this.keepSelected('selectedPackageIds', new Set(packages.map((p) => p.id)));
      });
  }

  private loadCatalogue(countryId: string | null): void {
    this.catalogueCountryId = countryId;
    this.store.dispatch(loadCustomerServices(countryId));
    this.store.dispatch(loadCustomerPackages(countryId));
  }

  /** Only a list priced for the country the form is priced in may trim the selection. */
  private pricedForAddress(countryId: string | null): boolean {
    return countryId !== null && countryId === this.addressCountryId();
  }

  private keepSelected(
    field: 'selectedServiceIds' | 'selectedPackageIds',
    offered: Set<string | undefined>,
  ): void {
    const selected = this.formData()[field];
    const kept = selected.filter((id) => offered.has(id));
    if (kept.length === selected.length) return;
    this.updateFormData({ [field]: kept });
    this.snackbar.showInfoTranslated('pages.order.wizard.catalogue_changed_for_country');
  }

  private async quote(
    serviceIds: string[],
    packageIds: string[],
    rooms: number,
    bathrooms: number,
    dirtinessLevel: DirtinessLevel,
    countryId: string | null,
  ): Promise<QuoteOrderResponse | null> {
    if (serviceIds.length === 0 && packageIds.length === 0) return null;
    const command = new QuoteOrderCommand();
    command.selectedServiceIds = serviceIds;
    command.selectedPackageIds = packageIds;
    command.rooms = rooms;
    command.bathrooms = bathrooms;
    command.dirtinessLevel = dirtinessLevel;
    // The country, never a currency: the server prices in the address country's currency, and
    // on create derives it from the saved address itself. A schedule has no express surcharge to
    // date-shift.
    command.countryId = countryId ?? undefined;
    command.currencyId = undefined;
    command.selectedExtraSlugs = [];
    command.cleaningDate = undefined;
    try {
      return (
        (await firstValueFrom(this.orderClient.quote(command).pipe(takeUntil(this.destroyed$)))) ??
        null
      );
    } catch {
      return null;
    }
  }

  /**
   * A catalogue item's name in the reader's language, or null while the
   * catalogue is still loading / if the item has since been retired.
   *
   * The translation lookup is inlined rather than imported from the order
   * wizard's helper: four other features already carry their own copy of these
   * six lines rather than reach across a feature boundary for them.
   */
  serviceName(id: string): string | null {
    return this.catalogName(this.services().find((s) => s.id === id));
  }

  packageName(id: string): string | null {
    return this.catalogName(this.packages().find((p) => p.id === id));
  }

  private catalogName(
    item: { name?: string; translations?: Record<string, unknown> } | undefined,
  ): string | null {
    if (!item) return null;
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    const bundle = item.translations?.[lang] as Record<string, string> | undefined;
    return bundle?.['name'] || item.name || null;
  }

  /**
   * The cleaners who have served this customer, asked with no slot: a schedule has no single instant,
   * so the roster says who may be asked, never who is free. A failed read hides the picker, as on the
   * one-off booking.
   */
  async loadServingCleaners(): Promise<void> {
    if (this.servingCleanersLoading()) return;
    this.servingCleanersLoading.set(true);
    try {
      const roster = await firstValueFrom(
        this.orderClient.myServingCleaners().pipe(takeUntil(this.destroyed$)),
      );
      this.servingCleaners.set(roster ?? []);
    } catch {
      this.servingCleaners.set([]);
    } finally {
      this.servingCleanersLoading.set(false);
    }
  }

  /** A new choice answers any refusal of the previous one. */
  selectPreferredCleaner(employeeId: string | null): void {
    this.preferredCleanerRefused.set(false);
    this.updateFormData({ preferredEmployeeId: employeeId });
  }

  /** Look one template up in the loaded list — the edit screen's entry point. */
  findTemplate(templateId: string): RecurringBookingTemplateDto | null {
    return this.templates().find((t) => t.id === templateId) ?? null;
  }

  /** The next instant this schedule fires, for the card's "next" line. */
  nextRun(template: RecurringBookingTemplateDto): Date | null {
    return nextOccurrenceUtc(template);
  }

  /** Load an existing template into the form so the same screen can edit it. */
  loadForEdit(template: RecurringBookingTemplateDto): void {
    this.editingId.set(template.id ?? null);
    this.formData.set({
      frequency: template.frequency,
      dayOfWeek: template.dayOfWeek,
      timeOfDay: template.timeOfDay ?? '10:00',
      rooms: template.rooms,
      bathrooms: template.bathrooms,
      dirtinessLevel: template.dirtinessLevel ?? DirtinessLevel.Normal,
      savedAddressId: template.savedAddressId ?? null,
      selectedServiceIds: [...(template.selectedServiceIds ?? [])],
      selectedPackageIds: [...(template.selectedPackageIds ?? [])],
      paymentType: template.paymentType,
      startsOn: template.startsOn ? new Date(template.startsOn) : null,
      endsOn: template.endsOn ? new Date(template.endsOn) : null,
      preferredEmployeeId: template.preferredEmployeeId ?? null,
      earlyPerformanceRequested: false,
    });
    this.preferredCleanerRefused.set(false);
    this.activeStep.set(1);
  }

  async refreshList(): Promise<void> {
    if (this.listLoading()) return;
    this.listLoading.set(true);
    try {
      const list = await firstValueFrom(this.client.getMine().pipe(takeUntil(this.destroyed$)));
      this.templates.set(list ?? []);
      this.listLoaded.set(true);
    } catch {
      this.snackbar.showError(this.translate.instant('recurring_booking.list_load_failed'));
    } finally {
      this.listLoading.set(false);
    }
  }

  // ─── Wizard mutators ───────────────────────────────────────────────
  updateFormData(patch: Partial<RecurringWizardFormData>): void {
    this.formData.update((current) => ({ ...current, ...patch }));
  }

  selectPayment(type: PaymentType): void {
    if (type === PaymentType.Cash && !this.cashSelectable()) return;
    this.cashCleared.set(false);
    this.updateFormData({ paymentType: type });
  }

  /** Never replaced by card: the customer is told and chooses again. */
  private dropCash(announce: boolean): void {
    this.updateFormData({ paymentType: null });
    this.cashCleared.set(true);
    if (announce) this.snackbar.showInfoTranslated('recurring_booking.cash_cleared');
  }

  setCardCaptureConsent(accepted: boolean): void {
    this.cardCapture.setConsent(accepted);
  }

  closeCardCapture(): void {
    this.cardCapture.close();
  }

  startCardCapture(): void {
    this.cardCapture.start();
  }

  private parkForm(): void {
    if (!this.isBrowser) return;
    try {
      const parked: ParkedForm = { templateId: this.editingId(), data: this.formData() };
      sessionStorage.setItem(PARKED_FORM_STORAGE_KEY, JSON.stringify(parked));
    } catch {
      // Storage refused: the customer comes back to the form as it loads, not as they left it.
    }
  }

  /**
   * The form as the customer left it to save a card, if it was this schedule (or a new one when
   * `templateId` is null). Reading consumes it.
   */
  restoreParkedForm(templateId: string | null): void {
    if (!this.isBrowser) return;
    let parked: ParkedForm | null = null;
    try {
      const raw = sessionStorage.getItem(PARKED_FORM_STORAGE_KEY);
      sessionStorage.removeItem(PARKED_FORM_STORAGE_KEY);
      parked = raw ? (JSON.parse(raw) as ParkedForm) : null;
    } catch {
      return;
    }
    if (!parked?.data || parked.templateId !== templateId) return;
    const { startsOn, endsOn } = parked.data;
    this.editingId.set(templateId);
    this.formData.set({
      ...parked.data,
      startsOn: startsOn ? new Date(startsOn) : null,
      endsOn: endsOn ? new Date(endsOn) : null,
    });
  }

  // ─── A package and a service it already includes ──────────────────
  //
  // Booked together they book that service twice on every clean, and nothing merges them; two
  // chosen packages that share a service do too. So the services list marks such a service, and a
  // tap that books one again asks first. A selection the form is handed (an order to repeat, a
  // schedule to edit, a parked form) is only marked.
  // → /product/business-rules#charging-a-package-and-a-service-together
  private readonly packagesByIncludedService = computed(() =>
    chosenPackagesByService(this.packages(), this.formData().selectedPackageIds),
  );

  /** The chosen packages that include a service, by name, or null when none does. */
  packageNamesIncluding(serviceId: string): string | null {
    const including = this.packagesByIncludedService().get(serviceId);
    if (!including) return null;
    return including.map((pkg) => this.catalogName(pkg) ?? '').join(', ');
  }

  /** Removing never asks; adding a service a chosen package includes does. */
  toggleService(id: string): void {
    const chosen = this.formData().selectedServiceIds;
    if (chosen.includes(id)) {
      this.updateFormData({ selectedServiceIds: chosen.filter((s) => s !== id) });
      return;
    }
    const packageNames = this.packageNamesIncluding(id);
    if (!packageNames) {
      this.addService(id);
      return;
    }
    // Already in two chosen packages, adding it is a third time, not "twice".
    const many = (this.packagesByIncludedService().get(id)?.length ?? 0) > 1;
    this.dialog
      .confirmTranslated(
        many
          ? 'pages.order.package_overlap.service_message_many'
          : 'pages.order.package_overlap.service_message',
        'pages.order.package_overlap.service_title',
        { service: this.serviceName(id) ?? '', package: packageNames },
        { acceptLabelKey: 'pages.order.package_overlap.add_again' },
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe((confirmed) => {
        if (confirmed) this.addService(id);
      });
  }

  /**
   * Removing never asks; adding a package that includes a service already in the form, on its own
   * or through another chosen package, does.
   */
  togglePackage(id: string): void {
    const chosen = this.formData().selectedPackageIds;
    if (chosen.includes(id)) {
      this.updateFormData({ selectedPackageIds: chosen.filter((p) => p !== id) });
      return;
    }
    const pkg = this.packages().find((p) => p.id === id);
    const overlap = includedServicesAlreadyChosen(pkg, this.packages(), this.formData());
    if (overlap.length === 0) {
      this.addPackage(id);
      return;
    }
    this.dialog
      .confirmTranslated(
        'pages.order.package_overlap.package_message',
        'pages.order.package_overlap.package_title',
        {
          package: this.catalogName(pkg) ?? '',
          services: overlap.map((s) => this.catalogName(s) ?? '').join(', '),
        },
        { acceptLabelKey: 'pages.order.package_overlap.add_package' },
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe((confirmed) => {
        if (confirmed) this.addPackage(id);
      });
  }

  // Read again on the answer: the form may have moved while the question was open.
  private addService(id: string): void {
    const chosen = this.formData().selectedServiceIds;
    if (!chosen.includes(id)) this.updateFormData({ selectedServiceIds: [...chosen, id] });
  }

  private addPackage(id: string): void {
    const chosen = this.formData().selectedPackageIds;
    if (!chosen.includes(id)) this.updateFormData({ selectedPackageIds: [...chosen, id] });
  }

  nextStep(): void {
    if (!this.canAdvance()) return;
    if (this.activeStep() < 3) this.activeStep.update((s) => s + 1);
  }

  prevStep(): void {
    if (this.activeStep() > 1) this.activeStep.update((s) => s - 1);
  }

  /** Reset wizard state — call on submit success or when leaving the screen. */
  resetWizard(): void {
    this.activeStep.set(1);
    this.editingId.set(null);
    this.formPrice.set(null);
    this.formCrew.set(null);
    this.cashCleared.set(false);
    this.preferredCleanerRefused.set(false);
    this.submitAttempted.set(false);
    this.formData.set({ ...RECURRING_WIZARD_INITIAL_DATA });
  }

  /**
   * Prefill the wizard from a past order once the catalogue priced for the address is on screen,
   * telling the customer what that list no longer offers. The prefill DID succeed then — the word
   * says what was dropped, not that it failed.
   */
  prefill(params: RecurringPrefillParams): void {
    this.pendingPrefill.set(params);
  }

  /**
   * Prefill the wizard from a past completed order, returning the names of anything no longer in the
   * catalog so the caller can say what was dropped.
   *
   * **Address, frequency and start date are NOT pre-filled** — they are the decisions that make the
   * template resolvable, and guessing them produces a schedule the materializer cannot honour.
   * → /flows/booking-and-pricing#recurring-bookings
   */
  prefillFromOrder(params: RecurringPrefillParams): string[] {
    const catalogServiceIds = new Set(
      this.services()
        .map((s) => s.id)
        .filter((id): id is string => !!id),
    );
    const catalogPackageIds = new Set(
      this.packages()
        .map((p) => p.id)
        .filter((id): id is string => !!id),
    );
    const catalogReady = catalogServiceIds.size > 0 || catalogPackageIds.size > 0;

    const keptServiceIds = catalogReady
      ? params.selectedServiceIds.filter((id) => catalogServiceIds.has(id))
      : params.selectedServiceIds;
    const keptPackageIds = catalogReady
      ? params.selectedPackageIds.filter((id) => catalogPackageIds.has(id))
      : params.selectedPackageIds;

    // Collect the names of dropped items so the caller can warn the user.
    const missing: string[] = [];
    if (catalogReady) {
      params.selectedServiceIds.forEach((id, i) => {
        if (!catalogServiceIds.has(id)) {
          missing.push(params.selectedServiceNames[i] || id);
        }
      });
      params.selectedPackageIds.forEach((id, i) => {
        if (!catalogPackageIds.has(id)) {
          missing.push(params.selectedPackageNames[i] || id);
        }
      });
    }

    this.updateFormData({
      selectedServiceIds: keptServiceIds,
      selectedPackageIds: keptPackageIds,
      rooms: params.rooms > 0 ? params.rooms : this.formData().rooms,
      bathrooms: params.bathrooms > 0 ? params.bathrooms : this.formData().bathrooms,
      paymentType: params.paymentType > 0 ? params.paymentType : this.formData().paymentType,
      timeOfDay: params.timeOfDay || this.formData().timeOfDay,
    });

    return missing;
  }

  /**
   * Submit the create command. Returns true on success — caller is
   * responsible for navigating + resetting the wizard. List cache is
   * refreshed in-place so the user lands on a fresh list.
   */
  submit(): Promise<boolean> {
    return this.save(false);
  }

  /**
   * The customer's own answer to a refused preferred cleaner — never taken on their behalf. The form
   * keeps the cleaner until this save goes through, so a save that never leaves or fails drops nothing.
   */
  saveWithoutPreferredCleaner(): Promise<boolean> {
    return this.save(true);
  }

  private async save(withoutPreferredCleaner: boolean): Promise<boolean> {
    if (this.submitting() || !this.canSubmit()) return false;
    const d = this.formData();
    const paymentType = d.paymentType;
    const dirtinessLevel = d.dirtinessLevel;
    if (!d.savedAddressId || !d.startsOn || paymentType === null || dirtinessLevel === null) {
      return false;
    }
    const editingId = this.editingId();
    const preferredEmployeeId = withoutPreferredCleaner
      ? undefined
      : (d.preferredEmployeeId ?? undefined);

    this.preferredCleanerRefused.set(false);
    this.submitting.set(true);
    try {
      if (paymentType === PaymentType.Cash && !(await this.cashConfirmedForForm())) return false;
      const saved = editingId
        ? await this.sendUpdate(editingId, d, paymentType, dirtinessLevel, preferredEmployeeId)
        : await this.sendCreate(d, paymentType, dirtinessLevel, preferredEmployeeId);
      if (withoutPreferredCleaner) this.updateFormData({ preferredEmployeeId: null });

      if (saved) {
        // Optimistic in-place write so the list is right the moment the user
        // lands back on it (avoids a flash of "no schedules yet", or of the
        // pre-edit values, if the network refresh races recomposition). The
        // stale price goes with it — the edit may have changed what it costs.
        if (editingId) {
          this.templatePrices.update((all) => {
            const rest = { ...all };
            delete rest[editingId];
            return rest;
          });
          this.templates.update((list) => list.map((t) => (t.id === editingId ? saved : t)));
        } else {
          this.templates.update((list) => [saved, ...list]);
        }
      }
      this.snackbar.showSuccess(
        this.translate.instant(
          editingId ? 'recurring_booking.update_success' : 'recurring_booking.create_success',
        ),
      );
      // Background refresh to pick up server-side enrichment (addressLine etc).
      this.refreshList();
      return true;
    } catch (error: unknown) {
      // The interceptor has already said why; a generic toast would replace that sentence.
      const code = extractApiErrorCode(error);
      if (code === 'order.cash_requires_saved_card') {
        this.cardCapture.open();
        return false;
      }
      if (code && CASH_REFUSALS.includes(code)) {
        this.dropCash(false);
        return false;
      }
      if (code === 'order.preferred_employee.not_eligible') {
        this.preferredCleanerRefused.set(true);
        return false;
      }
      this.snackbar.showError(
        this.translate.instant(
          editingId ? 'recurring_booking.update_failed' : 'recurring_booking.create_failed',
        ),
      );
      return false;
    } finally {
      this.submitting.set(false);
    }
  }

  /** Cash goes out only on a fresh quote for the form that says one cleaner does it. */
  private async cashConfirmedForForm(): Promise<boolean> {
    await this.quoteForm();
    if (this.cashSelectable()) return true;
    if (cashIsRefused(this.cashEligibility())) {
      this.dropCash(true);
    } else {
      this.snackbar.showError(this.translate.instant('recurring_booking.cash_unchecked'));
    }
    return false;
  }

  private sendCreate(
    d: RecurringWizardFormData,
    paymentType: PaymentType,
    dirtinessLevel: DirtinessLevel,
    preferredEmployeeId: string | undefined,
  ): Promise<RecurringBookingTemplateDto> {
    const command = new CreateRecurringBookingCommand();
    command.frequency = d.frequency as unknown as number;
    command.dayOfWeek = d.dayOfWeek;
    command.timeOfDay = d.timeOfDay;
    command.rooms = d.rooms;
    command.bathrooms = d.bathrooms;
    command.savedAddressId = d.savedAddressId ?? undefined;
    command.selectedServiceIds = d.selectedServiceIds;
    command.selectedPackageIds = d.selectedPackageIds;
    command.paymentType = paymentType;
    command.startsOn = d.startsOn as Date;
    command.endsOn = undefined;
    command.preferredEmployeeId = preferredEmployeeId;
    command.dirtinessLevel = dirtinessLevel;
    command.earlyPerformanceRequested = d.earlyPerformanceRequested ? true : undefined;
    return firstValueFrom(this.client.create(command).pipe(takeUntil(this.destroyed$)));
  }

  private sendUpdate(
    templateId: string,
    d: RecurringWizardFormData,
    paymentType: PaymentType,
    dirtinessLevel: DirtinessLevel,
    preferredEmployeeId: string | undefined,
  ): Promise<RecurringBookingTemplateDto> {
    const command = new UpdateRecurringBookingCommand();
    command.templateId = templateId;
    command.frequency = d.frequency as unknown as number;
    command.dayOfWeek = d.dayOfWeek;
    command.timeOfDay = d.timeOfDay;
    command.rooms = d.rooms;
    command.bathrooms = d.bathrooms;
    command.savedAddressId = d.savedAddressId ?? undefined;
    command.selectedServiceIds = d.selectedServiceIds;
    command.selectedPackageIds = d.selectedPackageIds;
    command.paymentType = paymentType;
    command.startsOn = d.startsOn as Date;
    command.endsOn = d.endsOn ?? undefined;
    command.preferredEmployeeId = preferredEmployeeId;
    command.dirtinessLevel = dirtinessLevel;
    return firstValueFrom(this.client.update(command).pipe(takeUntil(this.destroyed$)));
  }

  // ─── List actions ──────────────────────────────────────────────────
  async toggleActive(template: RecurringBookingTemplateDto): Promise<void> {
    if (!template.id || this.mutatingId()) return;
    this.mutatingId.set(template.id);
    try {
      const command = new SetRecurringBookingActiveCommand();
      command.templateId = template.id;
      command.isActive = !template.isActive;
      await firstValueFrom(this.client.setActive(command).pipe(takeUntil(this.destroyed$)));
      // Optimistic flip — saves a refresh round trip.
      this.templates.update((list) =>
        list.map((t) =>
          t.id === template.id
            ? Object.assign(new RecurringBookingTemplateDto(t), { isActive: !template.isActive })
            : t,
        ),
      );
    } catch {
      this.snackbar.showError(this.translate.instant('recurring_booking.toggle_failed'));
    } finally {
      this.mutatingId.set(null);
    }
  }

  async deleteTemplate(templateId: string): Promise<void> {
    if (this.mutatingId()) return;
    this.mutatingId.set(templateId);
    try {
      const command = new DeleteRecurringBookingCommand();
      command.templateId = templateId;
      await firstValueFrom(this.client.delete(command).pipe(takeUntil(this.destroyed$)));
      this.templates.update((list) => list.filter((t) => t.id !== templateId));
      this.snackbar.showSuccess(this.translate.instant('recurring_booking.delete_success'));
    } catch {
      this.snackbar.showError(this.translate.instant('recurring_booking.delete_failed'));
    } finally {
      this.mutatingId.set(null);
    }
  }
}
