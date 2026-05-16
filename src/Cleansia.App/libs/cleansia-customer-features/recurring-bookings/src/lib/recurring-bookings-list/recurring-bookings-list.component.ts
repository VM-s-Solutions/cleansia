import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { RecurringBookingTemplateDto } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { SkeletonModule } from 'primeng/skeleton';
import { RecurringBookingsFacade } from '../recurring-bookings.facade';
import { RecurrenceFrequency } from '../recurring-bookings.models';

/**
 * List view of the user's recurring booking templates. Plus-only feature —
 * the entry point on the Membership page is hidden for non-Plus users; this
 * screen itself doesn't double-gate.
 *
 * Two states:
 *  - Empty → marketing-style empty card with a primary CTA to the wizard.
 *  - Populated → mini cards (frequency, day+time, address, perk pills) with
 *    pause/resume + delete actions.
 */
@Component({
  selector: 'cleansia-customer-recurring-bookings-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    TranslatePipe,
    SkeletonModule,
    ConfirmDialogModule,
    CleansiaButtonComponent,
  ],
  providers: [RecurringBookingsFacade, ConfirmationService],
  templateUrl: './recurring-bookings-list.component.html',
})
export class RecurringBookingsListComponent implements OnInit {
  protected readonly facade = inject(RecurringBookingsFacade);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmationService);

  readonly templates = this.facade.templates;
  readonly loading = this.facade.listLoading;
  readonly loaded = this.facade.listLoaded;
  readonly mutatingId = this.facade.mutatingId;

  /** True while no data has loaded yet — drives the skeleton. */
  readonly initialLoading = computed(() => this.loading() && !this.loaded());

  ngOnInit(): void {
    this.facade.initialize();
  }

  goToCreate(): void {
    this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP, 'recurring', 'create']);
  }

  /**
   * Plain-language schedule heading: "Every week · Thursday at 10:00".
   * Backend dayOfWeek follows .NET DayOfWeek (Sun=0..Sat=6); JS Date uses
   * the same numbering, so we can index into the locale-aware day name list.
   */
  formatSchedule(template: RecurringBookingTemplateDto): string {
    const cadence = this.translate.instant(this.cadenceKey(template.frequency));
    const dayName = this.dayName(template.dayOfWeek);
    return this.translate.instant('recurring_booking.list_schedule_format', {
      cadence,
      day: dayName,
      time: template.timeOfDay ?? '',
    });
  }

  private cadenceKey(frequency: number): string {
    switch (frequency as RecurrenceFrequency) {
      case RecurrenceFrequency.Weekly:
        return 'recurring_booking.cadence_weekly';
      case RecurrenceFrequency.Biweekly:
        return 'recurring_booking.cadence_biweekly';
      case RecurrenceFrequency.Monthly:
        return 'recurring_booking.cadence_monthly';
      default:
        return 'recurring_booking.cadence_weekly';
    }
  }

  private dayName(dotNetDow: number): string {
    // .NET DayOfWeek: Sun=0, Mon=1, ..., Sat=6. We localize via the runtime's
    // toLocaleDateString — pick a known Sunday epoch (2024-01-07 was a Sunday)
    // and offset.
    const sundayEpoch = new Date('2024-01-07T12:00:00Z');
    const target = new Date(sundayEpoch);
    target.setUTCDate(sundayEpoch.getUTCDate() + dotNetDow);
    const lang = this.translate.currentLang || this.translate.getDefaultLang() || 'en';
    return target.toLocaleDateString(lang, { weekday: 'long' });
  }

  /** Pause / resume — flips the active flag via the backend. */
  toggleActive(template: RecurringBookingTemplateDto): void {
    this.facade.toggleActive(template);
  }

  /** Delete — opens the confirm dialog with explicit "what stops / what stays" copy. */
  confirmDelete(template: RecurringBookingTemplateDto): void {
    if (!template.id) return;
    const summary = this.formatSchedule(template);
    this.confirmService.confirm({
      header: this.translate.instant('recurring_booking.delete_dialog_title'),
      message: this.translate.instant('recurring_booking.delete_dialog_compound', { schedule: summary }),
      acceptLabel: this.translate.instant('recurring_booking.delete_dialog_confirm'),
      rejectLabel: this.translate.instant('global.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        if (template.id) this.facade.deleteTemplate(template.id);
      },
    });
  }

  /** Used by the template's @for trackBy. */
  trackById = (_: number, t: RecurringBookingTemplateDto): string => t.id ?? '';
}
