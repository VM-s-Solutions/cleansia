import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { CleansiaAddressAutocompleteComponent, CleansiaButtonComponent, CleansiaScrollTopComponent, CleansiaTelephoneComponent } from '@cleansia/components';
import { AddressDto, CategoryDto, CUSTOMER_API_BASE_URL, PackageListItem, QuoteOrderQuoteLine, PackageServiceSummary, PaymentType, SavedAddressDto, ServiceListItem } from '@cleansia/customer-services';
import type { MapboxAddressSuggestion } from '@cleansia/services';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { OrderWizardFacade } from './order-wizard.facade';
import { OrderMembershipFacade } from './order-membership.facade';
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
  formatPrice,
  generateTimeOptions,
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
import { WizardSummaryStepComponent } from './components/wizard-summary-step.component';

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
    WizardSummaryStepComponent,
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
    this.facade.applyAddressSuggestion(suggestion);
  }

  onAddressSearchFailed(): void {
    this.snackbar.showError(
      this.translate.instant('address_picker.search_failed')
    );
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

  onNextStep(): void {
    this.facade.nextStep();
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
