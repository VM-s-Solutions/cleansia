import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  CleansiaAddressAutocompleteComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { MapboxAddressSuggestion } from '@cleansia/services';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { RecurringBookingsFacade } from '../recurring-bookings.facade';
import {
  DAY_OF_WEEK_CHIPS,
  FREQUENCY_OPTIONS,
  MissingField,
  RecurrenceFrequency,
  RecurringPrefillParams,
  RECURRING_PREFILL_STORAGE_KEY,
  TIME_PERIOD_GROUPS,
} from '../recurring-bookings.models';

/**
 * One schedule, on one page — the board's "Nový rozvrh" artboard.
 *
 * It replaces a three-step wizard. The board collapses it because a schedule is
 * four decisions (what, how often, where, how it is paid) and all four fit on a
 * screen; stepping through them hid the price until the end, which is the one
 * number a customer is deciding on.
 *
 * The SAME screen edits an existing schedule, reached at
 * `/membership/recurring/:id`. Create and edit differ only in which command the
 * facade sends, so a second component would have been a second copy of this
 * form drifting from it.
 */
@Component({
  selector: 'cleansia-customer-create-recurring-wizard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslatePipe,
    FoamEdgeComponent,
    DatePickerModule,
    ConfirmDialogModule,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    CleansiaAddressAutocompleteComponent,
  ],
  providers: [RecurringBookingsFacade, ConfirmationService],
  templateUrl: './create-recurring-wizard.component.html',
})
export class CreateRecurringWizardComponent implements OnInit {
  protected readonly facade = inject(RecurringBookingsFacade);
  protected readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackbar = inject(SnackbarService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly FREQUENCY_OPTIONS = FREQUENCY_OPTIONS;
  protected readonly DAY_OF_WEEK_CHIPS = DAY_OF_WEEK_CHIPS;
  protected readonly RecurrenceFrequency = RecurrenceFrequency;

  /** Recurring schedules can't start in the past. */
  protected readonly minStartsOn = new Date();
  /** The booking wizard's own range — a schedule is a home, not a hotel. */
  protected readonly COUNTS = [0, 1, 2, 3, 4, 5, 6];
  protected readonly listRoute = ['/' + CleansiaCustomerRoute.MEMBERSHIP, 'recurring'];

  /** Flattened from the three period groups — the board draws one select. */
  protected readonly timeOptions = TIME_PERIOD_GROUPS.flatMap((group) =>
    group.slots.map((slot) => ({ label: slot, value: slot })),
  );

  // ─── Adding an address without leaving the form ────────────────────
  readonly addingAddress = signal(false);
  readonly savingAddress = signal(false);
  readonly pickedAddress = signal<MapboxAddressSuggestion | null>(null);
  newAddressLabel = '';

  readonly isEditing = computed(() => this.facade.editingId() !== null);

  readonly editingTemplate = computed(() => {
    const id = this.facade.editingId();
    return id ? this.facade.findTemplate(id) : null;
  });

  readonly addressOptions = computed(() =>
    this.facade.savedAddresses().map((address) => ({
      // "Home · Dělnická 12, Praha 7" — the label alone is not enough to tell
      // two saved addresses apart, which is the whole reason for the street.
      label: [address.label, [address.street, address.city].filter(Boolean).join(', ')]
        .filter(Boolean)
        .join(' · '),
      value: address.id,
    })),
  );

  /** 1 = Cash, 2 = Card — the backend's `PaymentType`. */
  readonly paymentOptions = computed(() => [
    { label: this.translate.instant('recurring_booking.pay_cash'), value: 1 },
    { label: this.translate.instant('recurring_booking.pay_card'), value: 2 },
  ]);

  /**
   * Re-quote whenever the priced inputs change. Watching those four fields
   * rather than the whole form object: the day, the time and the address move
   * no money, and quoting on each of them would put a request behind every tap.
   */
  private readonly quoteEffect = effect(() => {
    const d = this.facade.formData();
    void d.selectedServiceIds;
    void d.selectedPackageIds;
    void d.rooms;
    void d.bathrooms;
    this.facade.quoteForm();
  });

  /**
   * Path B prefill — set when the user arrives via "Make this recurring" on an
   * order detail page. Consumed once the catalogue is loaded so the
   * cross-check against live services/packages is meaningful.
   */
  private pendingPrefill = signal<RecurringPrefillParams | null>(null);

  private prefillEffect = effect(() => {
    const params = this.pendingPrefill();
    if (!params) return;

    const services = this.facade.services();
    const packages = this.facade.packages();
    const needsServices = params.selectedServiceIds.length > 0;
    const needsPackages = params.selectedPackageIds.length > 0;
    if ((needsServices && services.length === 0) || (needsPackages && packages.length === 0)) {
      return;
    }

    const missing = this.facade.prefillFromOrder(params);
    if (missing.length > 0) {
      // The prefill DID succeed — this says what was dropped, not that it failed.
      this.snackbar.showSuccess(
        this.translate.instant('recurring_booking.prefill_dropped_items', {
          items: missing.join(', '),
        }),
      );
    }
    this.pendingPrefill.set(null);
  });

  /**
   * Load the template being edited once the list is in memory. A deep link
   * lands here with nothing loaded, so this waits for `initialize()` rather
   * than reading the list in `ngOnInit`.
   */
  private readonly editEffect = effect(() => {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || this.facade.editingId() === id || !this.facade.listLoaded()) return;
    const template = this.facade.findTemplate(id);
    if (template) {
      this.facade.loadForEdit(template);
    } else {
      // The id is not one of this customer's schedules. Back to the list
      // rather than an empty form that would CREATE a second one on submit.
      this.router.navigate(this.listRoute);
    }
  });

  ngOnInit(): void {
    this.facade.initialize();

    const prefillFlag = this.route.snapshot.queryParamMap.get('prefill');
    if (prefillFlag === 'true' && this.isBrowser) {
      const raw = sessionStorage.getItem(RECURRING_PREFILL_STORAGE_KEY);
      if (raw) {
        sessionStorage.removeItem(RECURRING_PREFILL_STORAGE_KEY);
        try {
          this.pendingPrefill.set(JSON.parse(raw) as RecurringPrefillParams);
        } catch {
          // Corrupt payload — the user falls into the blank-slate flow.
        }
      }
    }
  }

  // ─── The price ─────────────────────────────────────────────────────
  formPrice(): string | null {
    const quoted = this.facade.formPrice();
    return quoted ? this.money(quoted.amount, quoted.currency) : null;
  }

  formatMoney(amount: number | undefined): string {
    return amount === undefined ? '' : this.money(amount, 'CZK');
  }

  private money(amount: number, currency: string): string {
    return new Intl.NumberFormat(this.locale(), {
      style: 'currency',
      currency,
      minimumFractionDigits: 0,
    }).format(amount);
  }

  private locale(): string {
    const map: Record<string, string> = {
      cs: 'cs-CZ',
      en: 'en-US',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    return map[this.translate.currentLang] || 'en-US';
  }

  // ─── Field handlers ────────────────────────────────────────────────
  // ─── Telling the customer what is missing ──────────────────────────
  /** A field's own message, shown only once they have pressed save. */
  showError(field: MissingField): boolean {
    return this.facade.submitAttempted() && this.facade.missing().includes(field);
  }

  showSummaryError(): boolean {
    return this.facade.submitAttempted() && this.facade.missing().length > 0;
  }

  /** "services, address" — the same names the field labels use. */
  missingLabels(): string {
    const keys: Record<MissingField, string> = {
      services: 'recurring_booking.field_services',
      time: 'recurring_booking.time_label',
      address: 'recurring_booking.address_label',
      startsOn: 'recurring_booking.starts_on_label',
    };
    return this.facade
      .missing()
      .map((field) => this.translate.instant(keys[field]).toLocaleLowerCase())
      .join(', ');
  }

  // ─── The inline address form ───────────────────────────────────────
  startAddingAddress(): void {
    this.addingAddress.set(true);
    this.pickedAddress.set(null);
    this.newAddressLabel = '';
  }

  cancelAddingAddress(): void {
    this.addingAddress.set(false);
    this.pickedAddress.set(null);
  }

  onAddressPicked(suggestion: MapboxAddressSuggestion): void {
    this.pickedAddress.set(suggestion);
  }

  async saveNewAddress(): Promise<void> {
    const picked = this.pickedAddress();
    if (!picked || this.savingAddress()) return;

    this.savingAddress.set(true);
    try {
      // An unnamed address is still an address — the city is a better fallback
      // than refusing to save one.
      const label = this.newAddressLabel.trim() || picked.city || picked.placeName;
      if (await this.facade.addAddress(label, picked)) {
        this.cancelAddingAddress();
      }
    } finally {
      this.savingAddress.set(false);
    }
  }

  selectFrequency(freq: RecurrenceFrequency): void {
    this.facade.updateFormData({ frequency: freq });
  }

  selectDay(dotNetDow: number): void {
    this.facade.updateFormData({ dayOfWeek: dotNetDow });
  }

  selectTime(slot: string): void {
    this.facade.updateFormData({ timeOfDay: slot });
  }

  setRooms(value: number | null): void {
    this.facade.updateFormData({ rooms: value ?? 0 });
  }

  setBathrooms(value: number | null): void {
    this.facade.updateFormData({ bathrooms: value ?? 0 });
  }

  toggleService(id: string): void {
    this.facade.toggleService(id);
  }

  togglePackage(id: string): void {
    this.facade.togglePackage(id);
  }

  isServiceSelected(id: string): boolean {
    return this.facade.formData().selectedServiceIds.includes(id);
  }

  isPackageSelected(id: string): boolean {
    return this.facade.formData().selectedPackageIds.includes(id);
  }

  selectAddress(id: string): void {
    this.facade.updateFormData({ savedAddressId: id });
  }

  selectPayment(type: number): void {
    this.facade.updateFormData({ paymentType: type });
  }

  onStartsOnChange(date: Date | null): void {
    this.facade.updateFormData({ startsOn: date });
  }

  // ─── Leaving the screen ────────────────────────────────────────────
  async submit(): Promise<void> {
    // Reveals the field messages from here on, whether or not this attempt goes
    // through — an incomplete form now answers instead of ignoring the press.
    this.facade.submitAttempted.set(true);
    if (this.facade.missing().length > 0) return;

    const ok = await this.facade.submit();
    if (ok) {
      this.facade.resetWizard();
      this.router.navigate(this.listRoute);
    }
  }

  async togglePause(): Promise<void> {
    const template = this.editingTemplate();
    if (!template) return;
    await this.facade.toggleActive(template);
  }

  confirmDelete(): void {
    const template = this.editingTemplate();
    if (!template?.id) return;
    this.confirmService.confirm({
      header: this.translate.instant('recurring_booking.delete_dialog_title'),
      message: this.translate.instant('recurring_booking.delete_dialog_compound', {
        schedule: this.translate.instant(
          this.cadenceKey(template.frequency),
        ),
      }),
      acceptLabel: this.translate.instant('recurring_booking.delete_dialog_confirm'),
      rejectLabel: this.translate.instant('global.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: async () => {
        const id = template.id as string;
        await this.facade.deleteTemplate(id);
        this.facade.resetWizard();
        this.router.navigate(this.listRoute);
      },
    });
  }

  private cadenceKey(frequency: number): string {
    switch (frequency as RecurrenceFrequency) {
      case RecurrenceFrequency.Biweekly:
        return 'recurring_booking.cadence_biweekly';
      case RecurrenceFrequency.Monthly:
        return 'recurring_booking.cadence_monthly';
      default:
        return 'recurring_booking.cadence_weekly';
    }
  }
}
