import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { WizardPreferredCleanerComponent } from './components/wizard-preferred-cleaner.component';
import { CleansiaAddressAutocompleteComponent, CleansiaButtonComponent, CleansiaScrollTopComponent, CleansiaTelephoneComponent } from '@cleansia/components';
import { AddressDto, CategoryDto, CUSTOMER_API_BASE_URL, GetMembershipPlansResponse, PackageListItem, PackageServiceSummary, PaymentType, QuoteOrderQuoteLine, QuotePlusSavingsQuery, SavedAddressDto, ServiceListItem, SignupConsentService } from '@cleansia/customer-services';
import type { MapboxAddressSuggestion } from '@cleansia/services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { OrderWizardFacade } from './order-wizard.facade';
import { OrderMembershipFacade } from './order-membership.facade';
import { OrderDraftService } from './order-draft.service';
import { OrderPreferredCleanerFacade } from './order-preferred-cleaner.facade';
import { OrderPricingFacade } from './order-pricing.facade';
import { OrderPromoFacade } from './order-promo.facade';
import { OrderSavedAddressFacade } from './order-saved-address.facade';
import { OrderServiceAreaFacade } from './order-service-area.facade';
import {
  RebookParams,
  TimeOption,
  createAddressDto,
  filterTimeOptionsForToday,
  composeSlotMoment,
  formatPrice,
  generateTimeOptions,
  PROMO_ERROR_FALLBACK,
  PROMO_ERROR_KEYS,
  getFieldError,
  getItemTranslation,
} from './order-wizard.models';
import {
  EXPRESS_LEAD_TIME_HOURS,
  EXPRESS_SURCHARGE_RATE,
  FIRST_WINDOW_HOUR,
  LAST_WINDOW_HOUR,
  STANDARD_LEAD_TIME_HOURS,
} from '@cleansia/models';

/** Midnight of a date, so two dates compare as days and not as instants. */
function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/** The first of a date's month. */
function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

@Component({
  selector: 'cleansia-customer-order-wizard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    InputTextModule,
    DatePickerModule,
    TextareaModule,
    SelectModule,
    DialogModule,
    CheckboxModule,
    CleansiaAddressAutocompleteComponent,
    CleansiaButtonComponent,
    CleansiaScrollTopComponent,
    CleansiaTelephoneComponent,
    WizardPreferredCleanerComponent,
    RouterModule,
  ],
  templateUrl: './order-wizard.component.html',
  providers: [
    OrderMembershipFacade,
    OrderPreferredCleanerFacade,
    OrderPricingFacade,
    OrderPromoFacade,
    OrderSavedAddressFacade,
    OrderServiceAreaFacade,
    OrderWizardFacade,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderWizardComponent implements OnInit {
  protected readonly facade = inject(OrderWizardFacade);
  protected readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly signupConsent = inject(SignupConsentService);
  private readonly draft = inject(OrderDraftService);
  private readonly router = inject(Router);
  private readonly apiBaseUrl = inject(CUSTOMER_API_BASE_URL, { optional: true }) ?? '';
  private readonly route = inject(ActivatedRoute);
  private readonly snackbar = inject(SnackbarService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  protected readonly PaymentType = PaymentType;

  mobileBreakdownExpanded = signal(false);
  showRebookWarning = signal(false);
  unavailableItems = signal<string[]>([]);
  private pendingRebook = signal<RebookParams | null>(null);
  saveNewAddress = signal(false);
  newAddressLabel = signal('');
  labelError = signal<string | null>(null);
  touched = signal<Record<string, boolean>>({});

  markTouched(field: string): void {
    this.touched.update((t) => ({ ...t, [field]: true }));
  }

  isTouched(field: string): boolean {
    return !!this.touched()[field];
  }

  fieldError(field: string): string | null {
    if (!this.isTouched(field)) return null;
    return getFieldError(field, this.facade.formData(), this.translate);
  }

  allTimeOptions: TimeOption[] = generateTimeOptions();

  /**
   * Today still has a slot someone could actually book.
   *
   * The lead time is part of that question. Without it this said yes to any hour
   * later than now, so the calendar offered today while every one of today's
   * slots was already inside the two-hour minimum — a date you can pick and then
   * cannot pair with a time.
   */
  private todayHasSlots(): boolean {
    const now = new Date();
    const earliestMinutes =
      now.getHours() * 60 + now.getMinutes() + EXPRESS_LEAD_TIME_HOURS * 60;
    return this.allTimeOptions.some((opt) => {
      const [h, m] = opt.value.split(':').map(Number);
      return h * 60 + m >= earliestMinutes;
    });
  }

  minDate = computed(() => {
    const today = new Date();
    if (this.todayHasSlots()) return today;
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    return tomorrow;
  });

  timeOptions = computed(() =>
    filterTimeOptionsForToday(this.allTimeOptions, this.facade.formData().cleaningDate)
  );

  /** The express-waiver note only belongs under a grid that actually offers an express slot. */
  hasExpressSlot = computed(() =>
    this.timeOptions().some((opt) => opt.availability === 'express')
  );

  selectedServices = computed(() => {
    const ids = this.facade.formData().selectedServiceIds;
    return this.facade.services().filter((s) => s.id && ids.includes(s.id));
  });

  selectedPackages = computed(() => {
    const ids = this.facade.formData().selectedPackageIds;
    return this.facade.packages().filter((p) => p.id && ids.includes(p.id));
  });

  private pendingServiceId = signal<string | null>(null);
  private pendingPackageId = signal<string | null>(null);

  private rebookEffect = effect(() => {
    const params = this.pendingRebook();
    if (!params) return;

    const services = this.facade.services();
    const packages = this.facade.packages();

    // Wait until both lists have loaded before attempting to match.
    // If the rebook references services, wait for services to load.
    // If it references packages, wait for packages to load.
    const needsServices = params.selectedServiceIds?.length > 0;
    const needsPackages = params.selectedPackageIds?.length > 0;
    if ((needsServices && services.length === 0) || (needsPackages && packages.length === 0)) return;

    const missing = this.facade.prefillFromRebook(params);
    if (missing.length > 0) {
      this.unavailableItems.set(missing);
      this.showRebookWarning.set(true);
    }
    this.pendingRebook.set(null);
  });

  private preselectEffect = effect(() => {
    const serviceId = this.pendingServiceId();
    const packageId = this.pendingPackageId();
    if (!serviceId && !packageId) return;

    const services = this.facade.services();
    const packages = this.facade.packages();
    if (services.length === 0 && packages.length === 0) return;

    const update: Partial<import('./order-wizard.models').OrderWizardFormData> = {};
    if (serviceId && services.some((s) => s.id === serviceId)) {
      update.selectedServiceIds = [serviceId];
      this.pendingServiceId.set(null);
    }
    if (packageId && packages.some((p) => p.id === packageId)) {
      update.selectedPackageIds = [packageId];
      this.pendingPackageId.set(null);
    }
    if (Object.keys(update).length > 0) {
      this.facade.updateFormData(update);
    }
  });

  /**
   * A query-param integer, or null when it is absent or not a sane count.
   * Capped at 20 — a URL is user input, and an absurd room count would drive a
   * pricing call and a form the page cannot render.
   */
  private readPositiveInt(key: string): number | null {
    const raw = this.route.snapshot.queryParamMap.get(key);
    if (raw === null) {
      return null;
    }
    const value = Number.parseInt(raw, 10);
    return Number.isInteger(value) && value >= 0 && value <= 20 ? value : null;
  }

  ngOnInit(): void {
    this.facade.initialize();

    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((e) => this.lang.set(e.lang));

    // The plans drive every number the Plus step prints, so they are wanted
    // before the customer reaches it rather than on arrival.
    this.facade.loadPlans();

    // A basket parked before a trip to sign-in comes back here. After
    // initialize(), because the restore checks what it holds against the
    // catalogue that call fetches.
    this.restoreDraft();

    // Pre-select service or package from query params (e.g., from services catalog)
    const serviceId = this.route.snapshot.queryParamMap.get('serviceId');
    const packageId = this.route.snapshot.queryParamMap.get('packageId');
    if (serviceId) {
      this.pendingServiceId.set(serviceId);
    }
    if (packageId) {
      this.pendingPackageId.set(packageId);
    }

    // The home-page calculator hands over everything the visitor chose there.
    // Before this the wizard read only serviceId, so a visitor who had already
    // picked a size and watched a price appear was asked for the size again.
    // Values are clamped rather than trusted: they arrive from a URL.
    const rooms = this.readPositiveInt('rooms');
    const bathrooms = this.readPositiveInt('bathrooms');
    const cleaningDate = this.route.snapshot.queryParamMap.get('cleaningDate');

    if (rooms !== null || bathrooms !== null) {
      this.facade.updateFormData({
        ...(rooms !== null ? { rooms } : {}),
        ...(bathrooms !== null ? { bathrooms } : {}),
      });
    }

    if (cleaningDate && !Number.isNaN(Date.parse(cleaningDate))) {
      this.facade.updateFormData({ cleaningDate: new Date(cleaningDate) });
    }

    const rebook = this.route.snapshot.queryParamMap.get('rebook');
    if (rebook === 'true' && this.isBrowser) {
      const raw = sessionStorage.getItem('cleansia_rebook_data');
      if (raw) {
        sessionStorage.removeItem('cleansia_rebook_data');
        try {
          this.pendingRebook.set(JSON.parse(raw));
        } catch {
          // Invalid rebook data, ignore
        }
      }
    }
  }

  /** Wired to the autocomplete component's `picked` output. */
  onAddressPicked(suggestion: MapboxAddressSuggestion): void {
    // A pick supersedes a manual entry: the lookup found it after all.
    this.facade.updateFormData({ addressEnteredManually: false });
    this.facade.applyAddressSuggestion(suggestion);
  }

  onAddressSearchFailed(): void {
    this.snackbar.showError(
      this.translate.instant('address_picker.search_failed')
    );
    // That message says the address can be typed instead. Until now it could
    // not — there was no input anywhere on the step. Open the fields with the
    // message that promises them.
    this.enterAddressManually();
  }

  /**
   * Switch to typing the address.
   *
   * Any coordinates from an abandoned pick are dropped: they belong to whatever
   * was highlighted last, not to what is about to be typed, and a cleaner sent
   * to the wrong pin is worse off than one sent to no pin at all.
   */
  enterAddressManually(): void {
    if (this.facade.formData().addressEnteredManually) return;
    this.facade.updateFormData({
      addressEnteredManually: true,
      addressLatitude: null,
      addressLongitude: null,
    });
  }

  /** Patch one field of the address without disturbing the others. */
  updateAddressField(field: 'street' | 'city' | 'zipCode', value: string): void {
    const current = this.facade.formData().address;
    this.facade.updateFormData({
      address: createAddressDto({
        street: current.street ?? '',
        city: current.city ?? '',
        zipCode: current.zipCode ?? '',
        countryId: current.countryId ?? '',
        state: current.state ?? '',
        [field]: value,
      }),
    });
  }

  isServiceSelected(id: string): boolean {
    return this.facade.formData().selectedServiceIds.includes(id);
  }

  toggleService(id: string): void {
    const current = this.facade.formData().selectedServiceIds;
    const updated = current.includes(id)
      ? current.filter((s) => s !== id)
      : [...current, id];
    this.facade.updateFormData({ selectedServiceIds: updated });
  }

  isPackageSelected(id: string): boolean {
    return this.facade.formData().selectedPackageIds.includes(id);
  }

  togglePackage(id: string): void {
    const current = this.facade.formData().selectedPackageIds;
    const updated = current.includes(id)
      ? current.filter((p) => p !== id)
      : [...current, id];
    this.facade.updateFormData({ selectedPackageIds: updated });
  }

  getServiceById(id: string): ServiceListItem | undefined {
    return this.facade.services().find((s) => s.id === id);
  }

  getPackageById(id: string): PackageListItem | undefined {
    return this.facade.packages().find((p) => p.id === id);
  }

  getTranslation(item: ServiceListItem | PackageListItem | PackageServiceSummary, field: string): string {
    return getItemTranslation(item, field, this.translate);
  }

  /**
   * Localized name for a category chip. Falls back to the default `name` when
   * the backend didn't send a translation for the active language. CategoryDto
   * has the same `translations` shape as ServiceListItem/PackageListItem so we
   * reuse the existing helper via a structural cast.
   */
  getCategoryName(cat: CategoryDto): string {
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    const translations = cat.translations;
    if (translations && translations[lang]) {
      const t = (translations[lang] as unknown as Record<string, string>)['name'];
      if (t) return t;
    }
    return cat.name ?? '';
  }

  /** Display name for the currently active filter, used in the "Filtering: X" note. */
  activeCategoryName = computed(() => {
    const slug = this.facade.selectedCategorySlug();
    if (!slug) return '';
    const cat = this.facade.categories().find((c) => c.slug === slug);
    return cat ? this.getCategoryName(cat) : '';
  });

  /**
   * Tapping the active chip clears the filter (mobile parity); tapping a different
   * chip switches to it. Selected services persist across filter changes — the
   * filter is purely visual.
   */
  onCategoryChipClick(slug: string | null): void {
    if (this.facade.selectedCategorySlug() === slug) {
      this.facade.setCategory(null);
    } else {
      this.facade.setCategory(slug);
    }
  }

  formatPrice(price: number): string {
    return formatPrice(price);
  }

  // Same icon rotation as the services-catalog page so both card sets read
  // as one system.
  private readonly serviceIcons = [
    'pi pi-home', 'pi pi-briefcase', 'pi pi-car',
    'pi pi-wrench', 'pi pi-building', 'pi pi-cog',
  ];
  private readonly packageIcons = ['pi pi-home', 'pi pi-star', 'pi pi-crown'];

  getServiceIcon(index: number): string {
    return this.serviceIcons[index % this.serviceIcons.length];
  }

  getPackageIcon(index: number): string {
    return this.packageIcons[index] ?? this.packageIcons[0];
  }

  /**
   * True once the customer has ASKED to go on and could not. Until then the step
   * says nothing about what is missing — a form that greets you red has told you
   * off for not having filled it in yet.
   *
   * Cleared on every step change, so the reasons belong to the step you are on
   * and to an attempt you actually made.
   */
  readonly triedToAdvance = signal(false);

  onNextStep(): void {
    if (this.blockingReasons().length > 0) {
      this.triedToAdvance.set(true);
      // The field-level errors come out with the summary. Blur is the only
      // thing that marks a field touched, so a field never visited has none —
      // which is exactly the field the customer needs pointed at.
      this.markStepFieldsTouched();
      return;
    }
    this.triedToAdvance.set(false);
    this.facade.nextStep();
  }

  /** Every field this step validates, whether or not it has been visited. */
  private markStepFieldsTouched(): void {
    if (this.facade.activeStep() !== 1) return;
    for (const field of [
      'customerFirstName',
      'customerLastName',
      'customerEmail',
      'customerPhone',
    ]) {
      this.markTouched(field);
    }
  }

  /** Leaving a step ends the attempt that belonged to it. */
  onPrevStep(): void {
    this.triedToAdvance.set(false);
    this.facade.prevStep();
  }

  isDateSelected = computed(() => !!this.facade.formData().cleaningDate);

  isTimeSelected(value: string): boolean {
    return this.facade.formData().cleaningTime === value;
  }

  selectTime(value: string): void {
    this.facade.updateFormData({ cleaningTime: value });
  }

  onDateChange(date: Date | null): void {
    this.facade.updateFormData({ cleaningDate: date });

    // Snap to the first BOOKABLE slot when the chosen one is not one on the new
    // date — the previous check accepted any slot in the list, which includes
    // the ones marked unavailable.
    const bookable = this.timeOptions().filter((o) => o.availability !== 'unavailable');
    const currentTime = this.facade.formData().cleaningTime;
    if (bookable.length && !bookable.some((o) => o.value === currentTime)) {
      this.facade.updateFormData({ cleaningTime: bookable[0].value });
    }
  }

  /**
   * The chosen slot is still bookable. `timeOptions()` MARKS availability rather
   * than filtering, so every slot is a member of it — including the ones already
   * inside the lead time. Membership alone let a customer advance on a slot the
   * backend would refuse.
   */
  hasValidTime = computed(() => {
    const currentTime = this.facade.formData().cleaningTime;
    return this.timeOptions().some(
      (o) => o.value === currentTime && o.availability !== 'unavailable'
    );
  });

  // ── step 3: when ────────────────────────────────────────────────────────────

  /**
   * The lead-time and surcharge rules, stated where the customer picks the time
   * that triggers them. All three mirror `BookingPolicy` and live in the shared
   * booking-window model, which the home calculator reads too — a second copy of
   * "2 to 4 hours is express" drifts the first time one of them changes.
   */
  readonly standardLeadHours = STANDARD_LEAD_TIME_HOURS;
  readonly expressLeadHours = EXPRESS_LEAD_TIME_HOURS;
  readonly expressRatePercent = Math.round(EXPRESS_SURCHARGE_RATE * 100);
  // Padded: the sentence sits under a grid of "08:00" chips, and "8:00–20:00"
  // beside them reads as a different kind of value.
  readonly firstWindowHour = String(FIRST_WINDOW_HOUR).padStart(2, '0');
  readonly lastWindowHour = String(LAST_WINDOW_HOUR).padStart(2, '0');

  /** The four ways in the artboard offers. Persisted on the order as a slug. */
  readonly accessModes = ['at_home', 'keys_handover', 'door_code', 'reception'] as const;

  setAccessMode(mode: string): void {
    this.facade.updateFormData({ accessMode: mode });
  }

  /** First of the month currently drawn. Not the selection — you can look ahead. */
  private readonly visibleMonth = signal(startOfMonth(new Date()));

  readonly visibleMonthLabel = computed(() => {
    const tag = this.lang() || this.translate.getDefaultLang() || 'cs';
    const label = new Intl.DateTimeFormat(tag, { month: 'long', year: 'numeric' })
      .format(this.visibleMonth());
    return label.charAt(0).toUpperCase() + label.slice(1);
  });

  /**
   * Weekday initials for the active locale, Monday first. Built from real dates
   * rather than a hardcoded list so a locale that abbreviates differently gets
   * its own — and so the order matches the grid, which is the part that breaks
   * silently if they disagree.
   */
  readonly weekdayNames = computed(() => {
    const tag = this.lang() || this.translate.getDefaultLang() || 'cs';
    const format = new Intl.DateTimeFormat(tag, { weekday: 'short' });
    // 2026-01-05 is a Monday.
    return Array.from({ length: 7 }, (_, i) => {
      const day = new Date(2026, 0, 5 + i);
      const name = format.format(day).replace('.', '');
      return name.charAt(0).toUpperCase() + name.slice(1);
    });
  });

  /**
   * The visible month as 7-column cells, with leading blanks so the first day
   * lands under its weekday. `bookable` is the same question the time grid asks
   * — a day with no slot left is not a day you can pick.
   */
  readonly calendarCells = computed(() => {
    const month = this.visibleMonth();
    const year = month.getFullYear();
    const monthIndex = month.getMonth();
    const daysInMonth = new Date(year, monthIndex + 1, 0).getDate();
    // getDay() is Sunday-first; the grid is Monday-first.
    const leading = (month.getDay() + 6) % 7;

    const earliest = startOfDay(this.minDate());
    const cells: { key: string; day: number | null; date: Date | null; bookable: boolean }[] = [];

    for (let i = 0; i < leading; i += 1) {
      cells.push({ key: `blank-${i}`, day: null, date: null, bookable: false });
    }
    for (let day = 1; day <= daysInMonth; day += 1) {
      const date = new Date(year, monthIndex, day);
      cells.push({
        key: `${year}-${monthIndex}-${day}`,
        day,
        date,
        bookable: startOfDay(date).getTime() >= earliest.getTime(),
      });
    }
    return cells;
  });

  /** No month is offered that is entirely behind the earliest bookable day. */
  readonly canGoPreviousMonth = computed(
    () => this.visibleMonth().getTime() > startOfMonth(this.minDate()).getTime()
  );

  shiftMonth(delta: number): void {
    const current = this.visibleMonth();
    this.visibleMonth.set(new Date(current.getFullYear(), current.getMonth() + delta, 1));
  }

  isSelectedDate(date: Date): boolean {
    const selected = this.facade.formData().cleaningDate;
    return !!selected && startOfDay(selected).getTime() === startOfDay(date).getTime();
  }

  selectDate(date: Date): void {
    this.onDateChange(date);
  }

  // ── step 5: Cleansia Plus ───────────────────────────────────────────────────

  readonly plusSavings = computed(() => this.facade.plusSavings());
  readonly servicesCatalogRoute = CleansiaCustomerRoute.SERVICES;

  /**
   * The mascot on the decline card. The artboard names mascot-arms-crossed,
   * which the project does not have — this is the nearest pose that exists, and
   * a missing image is a broken card.
   */
  readonly declineMascot = 'assets/images/mascot/mascot-ready.webp';

  /** The trial the plans actually grant, not a number typed into the copy. */
  readonly trialDays = computed(() => this.facade.plans()[0]?.trialPeriodDays ?? 0);

  /**
   * Services not already in the basket, three at most.
   *
   * The point of offering them HERE is that one visit covers them — so the list
   * is what this cleaner could also do on the trip already being booked, not a
   * catalogue.
   */
  readonly crossSellServices = computed(() => {
    const chosen = new Set(this.facade.formData().selectedServiceIds);
    return this.facade
      .services()
      .filter((service) => service.id && !chosen.has(service.id))
      .slice(0, 3);
  });

  /**
   * Ask the server what Plus is worth on the basket as it stands. Re-asked when
   * the basket changes, because the answer is a percentage OF it.
   */
  /**
   * Re-ask what Plus is worth whenever the basket that determines it changes,
   * and only while the step that shows the answer is on screen.
   */
  private readonly plusSavingsSync = effect(() => {
    const data = this.facade.formData();
    const onPlusStep = this.facade.activeStep() === 4;
    const havePlans = this.facade.plans().length > 0;
    // Read so the effect re-runs on a change to any of them.
    void data.selectedServiceIds;
    void data.selectedPackageIds;
    void data.rooms;
    void data.bathrooms;
    if (onPlusStep && havePlans) {
      this.refreshPlusSavings();
    }
  });

  private refreshPlusSavings(): void {
    const plan = this.facade.plans()[0];
    if (!plan?.code) return;

    const data = this.facade.formData();
    const query = new QuotePlusSavingsQuery();
    query.selectedServiceIds = data.selectedServiceIds;
    query.selectedPackageIds = data.selectedPackageIds;
    query.rooms = data.rooms;
    query.bathrooms = data.bathrooms;
    query.planCode = plan.code;
    // The SLOT, not the date: midnight is a different express band.
    query.cleaningDate = composeSlotMoment(data.cleaningDate, data.cleaningTime) ?? undefined;
    this.facade.loadPlusSavings(query);
  }

  /**
   * Take a plan.
   *
   * Signed in, this is the subscribe flow's job and it lives on the membership
   * screen — the wizard hands over rather than growing a second Stripe
   * integration. Signed out it cannot happen at all, so the customer is sent to
   * sign in with their basket parked.
   */
  choosePlan(plan: GetMembershipPlansResponse): void {
    if (!this.facade.isAuthenticated()) {
      this.goToAuth('login');
      return;
    }
    this.parkDraft();
    // /plus is the subscribe page now; the plan carries through so the page can
    // still be opened on the one the wizard was arguing for.
    this.router.navigate([CleansiaCustomerRoute.PLUS], {
      queryParams: { plan: plan.code },
    });
  }

  /**
   * Off to sign in or register, with the basket parked first.
   *
   * The step tells the customer their unfinished order will still be here. This
   * is the half of that sentence that is code — and it happens BEFORE the
   * navigation, because a draft saved on the way out is a draft lost to a slow
   * route.
   */
  goToAuth(target: 'login' | 'register'): void {
    this.parkDraft();
    this.router.navigate([
      target === 'login' ? CleansiaCustomerRoute.LOGIN : CleansiaCustomerRoute.REGISTER,
    ]);
  }

  private parkDraft(): void {
    this.draft.park(this.facade.activeStep(), this.facade.formData());
  }

  /**
   * Put a parked basket back, if there is one.
   *
   * The catalogue is NOT trusted to be the same: a service can be withdrawn
   * while the customer is signing in, and a basket restored with a price that no
   * longer exists is worse than one restored without it. Anything no longer on
   * offer is dropped and named, the same way a rebook does it.
   */
  private restoreDraft(): void {
    const parked = this.draft.take();
    if (!parked) return;

    const availableServices = new Set(
      this.facade.services().map((service) => service.id)
    );
    const availablePackages = new Set(
      this.facade.packages().map((pkg) => pkg.id)
    );

    const keptServices = parked.data.selectedServiceIds.filter((id) =>
      availableServices.has(id)
    );
    const keptPackages = parked.data.selectedPackageIds.filter((id) =>
      availablePackages.has(id)
    );
    const dropped =
      keptServices.length !== parked.data.selectedServiceIds.length ||
      keptPackages.length !== parked.data.selectedPackageIds.length;

    this.facade.updateFormData({
      ...parked.data,
      selectedServiceIds: keptServices,
      selectedPackageIds: keptPackages,
    });
    this.facade.goToStep(parked.step);

    if (dropped) {
      this.snackbar.showError(
        this.translate.instant('pages.order.draft_partially_restored')
      );
    }
  }

  // ── the review step ─────────────────────────────────────────────────────────

  /** Ticked before the order can be placed. Not a default — it is a consent. */
  readonly acceptedTerms = signal(false);

  /**
   * What was chosen, restated per step, each with a way back to the step that
   * owns it. Built from the form so it cannot describe a choice that is not
   * there — a review screen with its own copy of the answers is a second place
   * for them to be wrong.
   */
  readonly reviewCards = computed(() => {
    const data = this.facade.formData();
    const lines = this.priceLines();

    const services = lines.map(
      (line) => `${this.lineName(line)} — ${this.formatPrice(line.amount)}`
    );
    services.push(
      `${this.translate.instant(this.roomsKey(), { count: data.rooms })} · ` +
        `${this.translate.instant(this.bathroomsKey(), { count: data.bathrooms })}`
    );

    const unit = [data.customerFloor, data.customerApartment].filter(Boolean).join(', ');
    const address = [
      `${data.customerFirstName} ${data.customerLastName}`.trim(),
      [data.customerPhone, data.customerEmail].filter(Boolean).join(' · '),
      [
        [data.address.street, data.address.city, data.address.zipCode]
          .filter(Boolean)
          .join(', '),
        unit,
      ]
        .filter(Boolean)
        .join(' · '),
    ].filter(Boolean);

    const when: string[] = [];
    if (data.cleaningDate) {
      const tag = this.lang() || this.translate.getDefaultLang() || 'cs';
      when.push(
        `${new Intl.DateTimeFormat(tag, { dateStyle: 'long' }).format(data.cleaningDate)}, ${data.cleaningTime}`
      );
    }
    if (data.accessMode) {
      when.push(
        `${this.translate.instant('pages.order.access_mode_label')}: ` +
          this.translate.instant(`pages.order.access_mode.${data.accessMode}`)
      );
    }

    const pay = [
      this.translate.instant(
        data.paymentType === PaymentType.Card
          ? 'pages.order.payment_card_title'
          : 'pages.order.payment_cash_title'
      ),
    ];
    if (this.facade.promoCodeState().kind === 'valid') {
      pay.push(
        this.translate.instant('pages.order.promo.row_applied', {
          code: this.facade.promoCode(),
        })
      );
    }

    // The membership, if there is one by the time the order is reviewed — the
    // Plus step may have just added it. Named from the plan the server reports,
    // never from what the step offered: subscribing happens on Stripe's page and
    // can be abandoned there.
    const membership = this.facade.activeMembership();
    if (membership?.hasMembership && membership.planName) {
      pay.push(membership.planName);
    }

    return [
      { step: 0, icon: 'pi pi-list', titleKey: 'pages.order.steps.services', lines: services },
      { step: 1, icon: 'pi pi-map-marker', titleKey: 'pages.order.steps.address', lines: address },
      { step: 2, icon: 'pi pi-calendar', titleKey: 'pages.order.steps.datetime', lines: when },
      { step: 3, icon: 'pi pi-credit-card', titleKey: 'pages.order.review_payment_card', lines: pay },
    ];
  });

  /** Back to the step that owns a line, rather than back through all of them. */
  editStep(step: number): void {
    this.triedToAdvance.set(false);
    this.facade.goToStep(step);
  }

  // ── step 4: paying ──────────────────────────────────────────────────────────

  /** The two ways to pay, as equals — the price is the same either way. */
  readonly paymentMethods = [
    {
      type: PaymentType.Card,
      icon: 'pi pi-credit-card',
      titleKey: 'payment_card_title',
      descKey: 'payment_card_desc',
    },
    {
      type: PaymentType.Cash,
      icon: 'pi pi-wallet',
      titleKey: 'payment_cash_title',
      descKey: 'payment_cash_desc',
    },
  ] as const;

  /**
   * One round trip, on Apply. A promo code is validated by the server, so a
   * debounced check per keystroke is a request per keystroke for an answer only
   * the last one needs.
   */
  async applyPromo(): Promise<void> {
    const code = this.facade.promoCode().trim();
    if (!code) return;
    await this.facade.validatePromoCodeNow(code);
  }

  /**
   * What this order would have cost with no discount at all, and what it costs now.
   *
   * TWO TOTALS, never a subtotal-minus-discount chain. The express surcharge is computed on the
   * UNDISCOUNTED subtotal, so `subtotal − discount` does not reach the charged total on an express
   * order — `order-pricing.facade.ts` says so at length, and a row that fails to reconcile on
   * exactly the orders that cost most is worse than no row. `totalPrice` is the gross the server
   * quoted and `displayedTotalPrice` is what will be charged; both are the server's arithmetic, so
   * the pair is true whatever the surcharge is doing between them.
   */
  readonly hasSaving = computed(
    // Half a haler of tolerance: these are floats, and a rounding tail is not a discount.
    () => this.facade.totalPrice() - this.facade.displayedTotalPrice() > 0.005,
  );

  readonly priceBeforeDiscount = computed(() => formatPrice(this.facade.totalPrice()));

  readonly savingAmount = computed(() =>
    formatPrice(this.facade.totalPrice() - this.facade.displayedTotalPrice()),
  );

  /**
   * What a VALID code is actually worth to this order.
   *
   * `OrderFactory.ResolveLoy003Discount` — mirrored by `effectiveDiscount` — takes the LARGER of the
   * promo and the tier/membership pair; a promo never stacks on them. So a code can validate and
   * still change nothing, and saying only "applied" for both outcomes is how a customer ends up
   * believing a discount was taken that never was.
   */
  readonly promoOutcome = computed<'none' | 'applied' | 'superseded'>(() => {
    if (this.facade.promoCodeState().kind !== 'valid') return 'none';
    // `appliedDiscountKind` already resolves which source wins; asking it rather than
    // re-comparing the amounts keeps one answer to that question in the wizard.
    return this.facade.appliedDiscountKind() === 'promo' ? 'applied' : 'superseded';
  });

  /** The money a winning code takes off, ready to print. */
  readonly promoSavings = computed(() =>
    formatPrice(this.facade.effectivePromoDiscount()),
  );

  /** Drop the code and go back to whatever the order was worth without it. */
  removePromo(): void {
    this.facade.clearPromoCode();
  }

  /** Why the code was refused, in the customer's language. */
  readonly promoError = computed(() => {
    // Read so the message re-resolves on a language switch.
    this.lang();
    const state = this.facade.promoCodeState();
    if (state.kind !== 'invalid') return '';
    return this.translate.instant(
      PROMO_ERROR_KEYS[state.error ?? ''] ?? PROMO_ERROR_FALLBACK
    );
  });

  readonly propertyTypes = ['flat', 'house'] as const;

  /**
   * True once the static-map proxy has answered with something the browser
   * could not render — no token in this environment, or an upstream failure.
   * One flag, not a retry: the panel falls back to its empty state and stops
   * asking, rather than re-requesting a billed endpoint on every change.
   */
  private readonly mapFailed = signal(false);

  /**
   * The same-origin static map for the picked coordinates, or null when there
   * is nothing to show yet. Coordinates only exist after an autocomplete pick;
   * a typed address has none.
   */
  readonly mapUrl = computed(() => {
    if (this.mapFailed()) return null;
    const { addressLatitude: lat, addressLongitude: lng } = this.facade.formData();
    if (lat == null || lng == null) return null;
    // The API, like every other call this app makes. It answers with bytes, so
    // it is an <img src> rather than a generated-client call — but it is the
    // same host, the same rate limiter and the same credential as the search.
    return `${this.apiBaseUrl}/api/AddressSearch/map?lat=${lat}&lng=${lng}`;
  });

  onMapFailed(): void {
    this.mapFailed.set(true);
  }

  setPropertyType(propertyType: 'flat' | 'house'): void {
    // Clearing on the way to "house" is what makes the hidden fields honest —
    // otherwise a value typed before the switch is still in the form, and the
    // customer has no way to see or remove it.
    this.mapFailed.set(false);
    this.facade.updateFormData(
      propertyType === 'house'
        ? { propertyType, customerFloor: '', customerApartment: '' }
        : { propertyType }
    );
  }

  /** The counts the artboard offers. Rooms run to eight, bathrooms to four. */
  readonly roomChoices = [1, 2, 3, 4, 5, 6, 7, 8];
  readonly bathroomChoices = [1, 2, 3, 4];

  /**
   * Bumped on every language change. `translate.currentLang` is a plain
   * property, so a computed that reads it never re-runs and the plural form
   * would freeze in whatever language the page first loaded in.
   */
  readonly lang = signal(this.translate.currentLang);

  /**
   * Czech, Slovak, Russian and Ukrainian each take three plural forms and the
   * boundaries differ per language - Czech puts 5 in `other`, Russian in
   * `many`. `Intl.PluralRules` already knows every one of those rules, so the
   * category it returns picks the key rather than a hand-written threshold.
   * A single `{{rooms}} pokoje` read "1 pokoje" for the default one-room flat.
   */
  private pluralKey(base: string, count: number): string {
    const tag = this.lang() || this.translate.getDefaultLang() || 'cs';
    const cat = new Intl.PluralRules(tag).select(count);
    return `pages.order.${base}_${cat}`;
  }

  readonly roomsKey = computed(() => this.pluralKey('summary_rooms', this.facade.formData().rooms));
  readonly bathroomsKey = computed(() =>
    this.pluralKey('summary_bathrooms', this.facade.formData().bathrooms)
  );

  /**
   * Everything standing between this step and the next one, as translation keys.
   *
   * The facade owns the per-step conditions; the time slot is added here because
   * the valid slots depend on `timeOptions()`, which is the component's. The
   * advance button's disabled state reads THIS, so it can never be inert for a
   * reason the customer is not told.
   */
  readonly blockingReasons = computed(() => {
    const reasons = [...this.facade.missingReasons()];
    if (this.facade.activeStep() === 2 && !this.hasValidTime()) {
      reasons.push('pages.order.missing.time');
    }
    // The consent is the review step's own condition, and the place-order
    // button reads the same list as every other step so it cannot refuse
    // silently either. An account that already granted both is not asked, so
    // there is nothing for it to block on.
    if (
      this.facade.activeStep() === 5 &&
      !this.facade.alreadyConsented() &&
      !this.acceptedTerms()
    ) {
      reasons.push('pages.order.missing.terms');
    }
    return reasons;
  });

  /**
   * The quote's priced rows. Empty until the first quote comes back, which is
   * also when there is nothing selected to price.
   */
  readonly priceLines = computed(() => this.facade.quote()?.lines ?? []);

  /**
   * A row's display name, resolved from the catalogue this app already holds so
   * the name arrives translated. The quote carries ids, not names — a name on
   * the wire would be one the server had to translate, in a language it only
   * knows because the client told it.
   */
  /**
   * Take a line back out of the order, from the summary itself.
   *
   * The summary was read-only, so undoing an accidental tap meant going BACK
   * through the wizard to the step that added it — and by the review step that
   * is three screens away. Every kind on a quote line is something the customer
   * chose and can unchoose, so each one can be removed where they can see it.
   */
  removeLine(line: QuoteOrderQuoteLine): void {
    const id = line.itemId;
    if (!id) return;
    if (line.kind === 'package') this.togglePackage(id);
    else if (line.kind === 'service') this.toggleService(id);
    else this.facade.toggleExtra(id);
  }

  /** What the remove control announces, named for the line it removes. */
  removeLineLabel(line: QuoteOrderQuoteLine): string {
    return this.translate.instant('pages.order.summary_remove_item', {
      item: this.lineName(line),
    });
  }

  lineName(line: QuoteOrderQuoteLine): string {
    if (line.kind === 'package') {
      const pkg = this.facade.packages().find((p) => p.id === line.itemId);
      return pkg ? this.getTranslation(pkg, 'name') : '';
    }
    if (line.kind === 'service') {
      const service = this.facade.services().find((s) => s.id === line.itemId);
      return service ? this.getTranslation(service, 'name') : '';
    }
    const extra = this.facade.extras().find((e) => e.slug === line.itemId);
    return extra?.name ?? line.itemId ?? '';
  }

  /**
   * The quote's minute estimate, in hours to the nearest half, formatted for the
   * active locale — Czech writes a half hour as "2,5", and the raw number went
   * straight into the sentence as "2.5".
   */
  readonly estimateHours = computed(() => {
    const minutes = this.facade.quote()?.estimatedDurationMinutes ?? 0;
    if (!minutes) return null;
    const hours = Math.round((minutes / 60) * 2) / 2;
    const tag = this.lang() || this.translate.getDefaultLang() || 'cs';
    return new Intl.NumberFormat(tag, { maximumFractionDigits: 1 }).format(hours);
  });

  setRooms(rooms: number): void {
    this.facade.updateFormData({ rooms });
  }

  setBathrooms(bathrooms: number): void {
    this.facade.updateFormData({ bathrooms });
  }

  incrementRooms(): void {
    const current = this.facade.formData().rooms;
    this.facade.updateFormData({ rooms: current + 1 });
  }

  decrementRooms(): void {
    const current = this.facade.formData().rooms;
    if (current > 1) {
      this.facade.updateFormData({ rooms: current - 1 });
    }
  }

  incrementBathrooms(): void {
    const current = this.facade.formData().bathrooms;
    this.facade.updateFormData({ bathrooms: current + 1 });
  }

  decrementBathrooms(): void {
    const current = this.facade.formData().bathrooms;
    if (current > 1) {
      this.facade.updateFormData({ bathrooms: current - 1 });
    }
  }

  /**
   * Which address this actually is. A customer names a saved address once —
   * "Home", "Work" — and then has to remember what they meant; the street is
   * the only thing that tells two of them apart.
   */
  addressLineOf(address: SavedAddressDto): string {
    return [address.street, address.city].filter(Boolean).join(', ');
  }

  isAddressSelected(addr: SavedAddressDto): boolean {
    return this.facade.selectedSavedAddressId() === addr.id;
  }

  isCustomAddress(): boolean {
    return this.facade.selectedSavedAddressId() === null;
  }

  clearAddress(): void {
    this.saveNewAddress.set(false);
    this.newAddressLabel.set('');
    this.labelError.set(null);
    this.facade.selectedSavedAddressId.set(null);
    this.facade.updateFormData({
      address: createAddressDto(),
      addressLatitude: null,
      addressLongitude: null,
      addressEnteredManually: false,
    });
  }

  toggleSaveNewAddress(value: boolean): void {
    this.saveNewAddress.set(value);
    if (!value) {
      this.newAddressLabel.set('');
      this.labelError.set(null);
    }
  }

  async onPlaceOrder(): Promise<void> {
    if (this.blockingReasons().length > 0) {
      this.triedToAdvance.set(true);
      return;
    }
    // Parked BEFORE the submit, not after: the order can succeed and navigate
    // away, and a consent recorded only on the way out is a consent lost to a
    // slow network. The service delivers it at the first session that can take
    // one, so an anonymous booking's tick is not dropped either.
    // Only when one was actually taken. An account that already holds both was
    // not asked, and re-recording the same grant writes a consent nobody gave
    // on this screen.
    if (!this.facade.alreadyConsented()) {
      this.signupConsent.record(this.facade.formData().customerEmail);
    }

    if (this.saveNewAddress() && this.isCustomAddress()) {
      const label = this.newAddressLabel().trim();
      if (!label) {
        this.labelError.set(
          this.translate.instant('pages.order.address_label_required')
        );
        return;
      }
      this.labelError.set(null);
      await this.facade.submitOrder({ label });
      return;
    }
    await this.facade.submitOrder(null);
  }
}
