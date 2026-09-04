import { computed, inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddSavedAddressCommand,
  CreateRecurringBookingCommand,
  CustomerClient,
  DeleteRecurringBookingCommand,
  PackageListItem,
  QuoteOrderCommand,
  RecurringBookingTemplateDto,
  ServiceListItem,
  SetRecurringBookingActiveCommand,
  UpdateRecurringBookingCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom, takeUntil } from 'rxjs';
import {
  RecurringPrefillParams,
  RecurringWizardFormData,
  RECURRING_WIZARD_INITIAL_DATA,
  canAdvance,
  canSubmit,
  missingFields,
  nextOccurrenceUtc,
} from './recurring-bookings.models';

/**
 * Single facade for both the recurring-bookings list view and the create
 * wizard. Signal-only state — matches the order-wizard convention; no NgRx
 * slice unless cross-screen caching becomes valuable.
 *
 * Lifetime: provided at the *list* component scope so the templates cache
 * outlives the wizard navigation. The wizard itself doesn't re-provide it,
 * so tapping "Create" → submit → back-to-list reuses the same in-flight
 * cache without a re-fetch round trip.
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
  private readonly translate = inject(TranslateService);
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly store = inject(Store);

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

  // ─── Prices ────────────────────────────────────────────────────────
  /** templateId → quoted price per clean. Absent until the quote lands. */
  readonly templatePrices = signal<Record<string, { amount: number; currency: string }>>({});
  /** The price of whatever the form currently describes. */
  readonly formPrice = signal<{ amount: number; currency: string } | null>(null);
  readonly quoting = signal(false);

  /** Drives the address field's own spinner — see `ensureAddresses`. */
  readonly addressesLoading = signal(false);

  // ─── Wizard state ──────────────────────────────────────────────────
  readonly activeStep = signal(1);
  readonly formData = signal<RecurringWizardFormData>({
    ...RECURRING_WIZARD_INITIAL_DATA,
  });
  readonly submitting = signal(false);

  // ─── Shared catalog + addresses (reused across both screens) ───────
  readonly services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });
  readonly packages = toSignal(this.store.select(selectCustomerPackages), {
    initialValue: [] as PackageListItem[],
  });
  readonly savedAddresses = this.savedAddressStore.addresses;

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
  readonly missing = computed(() => missingFields(this.formData()));

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

    // Catalog dispatches are no-ops on already-loaded state. They flow into
    // the customer-stores reducers, populating the signals above.
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());

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
      this.isMember.set(me?.hasMembership === true);
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
    );
    if (!quoted) return;
    this.templatePrices.update((all) => ({ ...all, [template.id as string]: quoted }));
  }

  /** Re-quote whatever the form currently describes. */
  async quoteForm(): Promise<void> {
    const d = this.formData();
    if (d.selectedServiceIds.length === 0 && d.selectedPackageIds.length === 0) {
      this.formPrice.set(null);
      return;
    }
    this.quoting.set(true);
    try {
      this.formPrice.set(
        await this.quote(d.selectedServiceIds, d.selectedPackageIds, d.rooms, d.bathrooms),
      );
    } finally {
      this.quoting.set(false);
    }
  }

  private async quote(
    serviceIds: string[],
    packageIds: string[],
    rooms: number,
    bathrooms: number,
  ): Promise<{ amount: number; currency: string } | null> {
    if (serviceIds.length === 0 && packageIds.length === 0) return null;
    const command = new QuoteOrderCommand();
    command.selectedServiceIds = serviceIds;
    command.selectedPackageIds = packageIds;
    command.rooms = rooms;
    command.bathrooms = bathrooms;
    // Left unset deliberately: the currency is the caller's, resolved server
    // side, and a schedule has no express surcharge to date-shift.
    command.currencyId = undefined;
    command.selectedExtraSlugs = [];
    command.cleaningDate = undefined;
    try {
      const quoted = await firstValueFrom(
        this.orderClient.quote(command).pipe(takeUntil(this.destroyed$)),
      );
      if (!quoted) return null;
      return {
        amount: quoted.finalPriceAfterDiscount ?? quoted.totalPrice,
        currency: quoted.currencyCode || 'CZK',
      };
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
      savedAddressId: template.savedAddressId ?? null,
      selectedServiceIds: [...(template.selectedServiceIds ?? [])],
      selectedPackageIds: [...(template.selectedPackageIds ?? [])],
      paymentType: template.paymentType,
      startsOn: template.startsOn ? new Date(template.startsOn) : null,
    });
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

  toggleService(id: string): void {
    const current = this.formData().selectedServiceIds;
    this.updateFormData({
      selectedServiceIds: current.includes(id)
        ? current.filter((s) => s !== id)
        : [...current, id],
    });
  }

  togglePackage(id: string): void {
    const current = this.formData().selectedPackageIds;
    this.updateFormData({
      selectedPackageIds: current.includes(id)
        ? current.filter((p) => p !== id)
        : [...current, id],
    });
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
    this.submitAttempted.set(false);
    this.formData.set({ ...RECURRING_WIZARD_INITIAL_DATA });
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
  async submit(): Promise<boolean> {
    if (this.submitting() || !this.canSubmit()) return false;
    const d = this.formData();
    if (!d.savedAddressId || !d.startsOn) return false;
    const editingId = this.editingId();

    this.submitting.set(true);
    try {
      const saved = editingId
        ? await this.sendUpdate(editingId, d)
        : await this.sendCreate(d);

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
    } catch {
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

  private sendCreate(d: RecurringWizardFormData): Promise<RecurringBookingTemplateDto> {
    const command = new CreateRecurringBookingCommand();
    command.frequency = d.frequency as unknown as number;
    command.dayOfWeek = d.dayOfWeek;
    command.timeOfDay = d.timeOfDay;
    command.rooms = d.rooms;
    command.bathrooms = d.bathrooms;
    command.savedAddressId = d.savedAddressId ?? undefined;
    command.selectedServiceIds = d.selectedServiceIds;
    command.selectedPackageIds = d.selectedPackageIds;
    command.paymentType = d.paymentType;
    command.startsOn = d.startsOn as Date;
    command.endsOn = undefined;
    return firstValueFrom(this.client.create(command).pipe(takeUntil(this.destroyed$)));
  }

  private sendUpdate(
    templateId: string,
    d: RecurringWizardFormData,
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
    command.paymentType = d.paymentType;
    command.startsOn = d.startsOn as Date;
    command.endsOn = undefined;
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
