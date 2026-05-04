import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePickerModule } from 'primeng/datepicker';
import { RecurringBookingsFacade } from '../recurring-bookings.facade';
import {
  DAY_OF_WEEK_CHIPS,
  FREQUENCY_OPTIONS,
  RecurrenceFrequency,
  RecurringPrefillParams,
  RECURRING_PREFILL_STORAGE_KEY,
  TIME_PERIOD_GROUPS,
} from '../recurring-bookings.models';

/**
 * Three-step wizard for creating a RecurringBookingTemplate. Mirrors the
 * mobile flow:
 *
 *  Step 1 — When:  Frequency · Day-of-week · Time-of-day
 *  Step 2 — What:  Packages · Services · Rooms · Bathrooms
 *  Step 3 — Where & Pay:  Address · Payment · Starts on
 *
 * Wave A: Path A (blank-slate) only. Path B (pre-fill from a past Completed
 * order) lands in a follow-up.
 */
@Component({
  selector: 'cleansia-customer-create-recurring-wizard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    DatePickerModule,
    CleansiaButtonComponent,
  ],
  // NOTE: facade is provided here too because the wizard can be loaded as a
  // direct route entry (deep link). When entered from the list it will use
  // the existing instance via Angular's hierarchical injection.
  providers: [RecurringBookingsFacade],
  templateUrl: './create-recurring-wizard.component.html',
})
export class CreateRecurringWizardComponent implements OnInit {
  protected readonly facade = inject(RecurringBookingsFacade);
  protected readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackbar = inject(SnackbarService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Static metadata referenced from the template.
  protected readonly FREQUENCY_OPTIONS = FREQUENCY_OPTIONS;
  protected readonly DAY_OF_WEEK_CHIPS = DAY_OF_WEEK_CHIPS;
  protected readonly TIME_PERIOD_GROUPS = TIME_PERIOD_GROUPS;
  protected readonly RecurrenceFrequency = RecurrenceFrequency;

  /** Min date for the picker — today. Recurring schedules can't start in the past. */
  protected readonly minStartsOn = new Date();

  /**
   * Path B prefill — set when the user arrives via "Make this recurring"
   * on an order detail page. The order-detail stashes the payload in
   * sessionStorage and navigates with `?prefill=true`. We pull it once
   * on init, then consume it as soon as the catalog loads (effect below)
   * so the cross-check against the active service/package list works.
   */
  private pendingPrefill = signal<RecurringPrefillParams | null>(null);

  private prefillEffect = effect(() => {
    const params = this.pendingPrefill();
    if (!params) return;

    // Wait for catalog to load before attempting the cross-check —
    // otherwise we'd erroneously drop every prefilled item.
    const services = this.facade.services();
    const packages = this.facade.packages();
    const needsServices = params.selectedServiceIds.length > 0;
    const needsPackages = params.selectedPackageIds.length > 0;
    if ((needsServices && services.length === 0) || (needsPackages && packages.length === 0)) {
      return;
    }

    const missing = this.facade.prefillFromOrder(params);
    if (missing.length > 0) {
      // SnackbarService doesn't expose a separate "info" channel — use the
      // success channel because the prefill DID succeed; we're just
      // informing the user we dropped some items that no longer exist.
      this.snackbar.showSuccess(
        this.translate.instant('recurring_booking.prefill_dropped_items', {
          items: missing.join(', '),
        }),
      );
    }
    this.pendingPrefill.set(null);
  });

  /**
   * Live-summary sentence that mirrors the mobile banner. Updates on every
   * tap so users see "what they just chose" without scrolling. Format:
   * "Every Thursday at 10:00".
   */
  readonly summarySentence = computed(() => {
    const data = this.facade.formData();
    const cadenceKey = this.cadenceTemplateKey(data.frequency);
    const day = this.dayName(data.dayOfWeek);
    return this.translate.instant(cadenceKey, { day, time: data.timeOfDay || '—' });
  });

  ngOnInit(): void {
    this.facade.initialize();

    // Path B — pull the prefill payload stashed by order-detail. One-shot
    // per visit (the sessionStorage entry is removed after read so a
    // refresh / back-nav doesn't re-apply stale data).
    const prefillFlag = this.route.snapshot.queryParamMap.get('prefill');
    if (prefillFlag === 'true' && this.isBrowser) {
      const raw = sessionStorage.getItem(RECURRING_PREFILL_STORAGE_KEY);
      if (raw) {
        sessionStorage.removeItem(RECURRING_PREFILL_STORAGE_KEY);
        try {
          this.pendingPrefill.set(JSON.parse(raw) as RecurringPrefillParams);
        } catch {
          // Corrupt payload — ignore silently, user falls into the blank-slate flow.
        }
      }
    }
  }

  // ─── Step navigation ───────────────────────────────────────────────
  goToStep(step: number): void {
    // Only allow jumping back to a previous step — forward navigation
    // requires passing canAdvance gates progressively.
    if (step < this.facade.activeStep()) {
      this.facade.activeStep.set(step);
    }
  }

  onNext(): void {
    if (this.facade.activeStep() < 3) {
      this.facade.nextStep();
    } else {
      this.submit();
    }
  }

  onBack(): void {
    if (this.facade.activeStep() > 1) {
      this.facade.prevStep();
    } else {
      this.cancel();
    }
  }

  cancel(): void {
    this.facade.resetWizard();
    this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP, 'recurring']);
  }

  async submit(): Promise<void> {
    const ok = await this.facade.submit();
    if (ok) {
      this.facade.resetWizard();
      this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP, 'recurring']);
    }
  }

  // ─── Step 1 helpers ────────────────────────────────────────────────
  selectFrequency(freq: RecurrenceFrequency): void {
    this.facade.updateFormData({ frequency: freq });
  }

  selectDay(dotNetDow: number): void {
    this.facade.updateFormData({ dayOfWeek: dotNetDow });
  }

  selectTime(slot: string): void {
    this.facade.updateFormData({ timeOfDay: slot });
  }

  // ─── Step 2 helpers ────────────────────────────────────────────────
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

  incrementRooms(): void {
    this.facade.updateFormData({ rooms: this.facade.formData().rooms + 1 });
  }

  decrementRooms(): void {
    const v = this.facade.formData().rooms;
    if (v > 0) this.facade.updateFormData({ rooms: v - 1 });
  }

  incrementBathrooms(): void {
    this.facade.updateFormData({ bathrooms: this.facade.formData().bathrooms + 1 });
  }

  decrementBathrooms(): void {
    const v = this.facade.formData().bathrooms;
    if (v > 0) this.facade.updateFormData({ bathrooms: v - 1 });
  }

  // ─── Step 3 helpers ────────────────────────────────────────────────
  selectAddress(id: string): void {
    this.facade.updateFormData({ savedAddressId: id });
  }

  selectPayment(type: number): void {
    this.facade.updateFormData({ paymentType: type });
  }

  onStartsOnChange(date: Date | null): void {
    this.facade.updateFormData({ startsOn: date });
  }

  // ─── Localization helpers ──────────────────────────────────────────
  private cadenceTemplateKey(freq: RecurrenceFrequency): string {
    switch (freq) {
      case RecurrenceFrequency.Weekly:
        return 'recurring_booking.summary_cadence_weekly';
      case RecurrenceFrequency.Biweekly:
        return 'recurring_booking.summary_cadence_biweekly';
      case RecurrenceFrequency.Monthly:
        return 'recurring_booking.summary_cadence_monthly';
    }
  }

  private dayName(dotNetDow: number): string {
    // Reuse the same localized weekday lookup as the list component — see
    // the comment there about the Sunday-epoch trick.
    const sundayEpoch = new Date('2024-01-07T12:00:00Z');
    const target = new Date(sundayEpoch);
    target.setUTCDate(sundayEpoch.getUTCDate() + dotNetDow);
    const lang = this.translate.currentLang || this.translate.getDefaultLang() || 'en';
    return target.toLocaleDateString(lang, { weekday: 'long' });
  }

  // Used by template @for trackBy
  trackByValue = (_: number, opt: { value: string | number }): string | number => opt.value;
}
