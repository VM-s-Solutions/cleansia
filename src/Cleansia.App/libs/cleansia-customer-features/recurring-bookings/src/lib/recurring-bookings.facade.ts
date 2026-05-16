import { computed, inject, Injectable, signal } from '@angular/core';
import {
  CreateRecurringBookingCommand,
  CustomerClient,
  DeleteRecurringBookingCommand,
  RecurringBookingTemplateDto,
  SetRecurringBookingActiveCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PackageListItem, ServiceListItem } from '@cleansia/partner-services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import {
  RecurringPrefillParams,
  RecurringWizardFormData,
  RECURRING_WIZARD_INITIAL_DATA,
  canAdvance,
  canSubmit,
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
export class RecurringBookingsFacade {
  // Always go through CustomerClient — injecting RecurringBookingClient
  // directly hits NSwag's empty-string default baseUrl, sending requests
  // back to the SPA's own origin instead of the configured API URL.
  private readonly customerClient = inject(CustomerClient);
  private readonly client = this.customerClient.recurringBookingClient;
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
   * Bootstrap: load templates + addresses + catalog. Safe to call on every
   * list-screen entry — internal `loaded` guards skip redundant fetches.
   */
  async initialize(): Promise<void> {
    // Catalog dispatches are no-ops on already-loaded state. They flow into
    // the customer-stores reducers, populating the signals above.
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());

    if (!this.savedAddressStore.loaded()) {
      await this.savedAddressStore.refresh();
    }
    await this.refreshList();

    // Default startsOn to one week from today on first wizard entry, so the
    // user doesn't see a blank field. They can change via the date picker.
    if (!this.formData().startsOn) {
      const nextWeek = new Date();
      nextWeek.setDate(nextWeek.getDate() + 7);
      nextWeek.setHours(0, 0, 0, 0);
      this.updateFormData({ startsOn: nextWeek });
    }

    // Default savedAddressId to the user's default address if any.
    if (!this.formData().savedAddressId) {
      const defaultAddr =
        this.savedAddresses().find((a) => a.isDefault) ?? this.savedAddresses()[0];
      if (defaultAddr?.id) {
        this.updateFormData({ savedAddressId: defaultAddr.id });
      }
    }
  }

  async refreshList(): Promise<void> {
    if (this.listLoading()) return;
    this.listLoading.set(true);
    try {
      const list = await firstValueFrom(this.client.getMine());
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
    this.formData.set({ ...RECURRING_WIZARD_INITIAL_DATA });
  }

  /**
   * Path B — prefill the wizard from a past Completed order. Returns the
   * names of any services/packages that no longer exist in the catalog so
   * the caller can show a "we dropped these from your prefill" snackbar.
   *
   * Mirrors the order-wizard's rebook prefill: cross-checks against the
   * loaded catalog, drops IDs that no longer exist, but keeps everything
   * else (rooms, bathrooms, payment type, time slot from the source order).
   *
   * Address + frequency + start date are NOT pre-filled — those are the
   * decisions the user actually has to make to convert a one-off into a
   * recurring schedule. Same UX as mobile.
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

    this.submitting.set(true);
    try {
      const command = new CreateRecurringBookingCommand({
        frequency: d.frequency as unknown as number,
        dayOfWeek: d.dayOfWeek,
        timeOfDay: d.timeOfDay,
        rooms: d.rooms,
        bathrooms: d.bathrooms,
        savedAddressId: d.savedAddressId,
        selectedServiceIds: d.selectedServiceIds,
        selectedPackageIds: d.selectedPackageIds,
        paymentType: d.paymentType,
        startsOn: d.startsOn,
        endsOn: undefined,
      });
      const created = await firstValueFrom(this.client.create(command));
      // Optimistic in-place insert so the list shows the new template
      // immediately when the user lands back on it (avoids a flash of
      // "no schedules yet" if the network refresh races recomposition).
      if (created) {
        this.templates.update((list) => [created, ...list]);
      }
      this.snackbar.showSuccess(this.translate.instant('recurring_booking.create_success'));
      // Background refresh to pick up server-side enrichment (addressLine etc).
      this.refreshList();
      return true;
    } catch {
      this.snackbar.showError(this.translate.instant('recurring_booking.create_failed'));
      return false;
    } finally {
      this.submitting.set(false);
    }
  }

  // ─── List actions ──────────────────────────────────────────────────
  async toggleActive(template: RecurringBookingTemplateDto): Promise<void> {
    if (!template.id || this.mutatingId()) return;
    this.mutatingId.set(template.id);
    try {
      const command = new SetRecurringBookingActiveCommand({
        templateId: template.id,
        isActive: !template.isActive,
      });
      await firstValueFrom(this.client.setActive(command));
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
      const command = new DeleteRecurringBookingCommand({
        templateId,
      });
      await firstValueFrom(this.client.delete(command));
      this.templates.update((list) => list.filter((t) => t.id !== templateId));
      this.snackbar.showSuccess(this.translate.instant('recurring_booking.delete_success'));
    } catch {
      this.snackbar.showError(this.translate.instant('recurring_booking.delete_failed'));
    } finally {
      this.mutatingId.set(null);
    }
  }
}
