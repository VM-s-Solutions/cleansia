import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { RecurringBookingTemplateDto } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { RecurringBookingsFacade } from '../recurring-bookings.facade';
import { RecurrenceFrequency } from '../recurring-bookings.models';

/**
 * The schedules a customer has — the board's "Moje rozvrhy" artboard, and its
 * "Bez členství" one, which is the same route seen without Plus.
 *
 * The paywall is not decoration. `CreateRecurringBooking` refuses a caller
 * without a membership (`RecurringTemplateMembershipRequired`), so the previous
 * behaviour — an empty list with a "new schedule" button — offered a button the
 * API would reject. → /product/business-rules
 *
 * Each row states a price, which is QUOTED per template rather than derived
 * here: the discount stack lives behind `Order/Quote` and a second copy of that
 * arithmetic would disagree the first time a rate moved. A quote that does not
 * come back leaves the row with no figure at all.
 */
@Component({
  selector: 'cleansia-customer-recurring-bookings-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, RouterLink, SkeletonModule, FoamEdgeComponent],
  providers: [RecurringBookingsFacade],
  templateUrl: './recurring-bookings-list.component.html',
})
export class RecurringBookingsListComponent implements OnInit {
  protected readonly facade = inject(RecurringBookingsFacade);
  private readonly translate = inject(TranslateService);

  readonly templates = this.facade.templates;
  readonly loading = this.facade.listLoading;
  readonly loaded = this.facade.listLoaded;
  readonly isMember = this.facade.isMember;

  protected readonly createRoute = ['/' + CleansiaCustomerRoute.MEMBERSHIP, 'recurring', 'create'];
  protected readonly plusRoute = '/' + CleansiaCustomerRoute.PLUS;

  /** True until the entitlement AND the first list response have landed. */
  readonly initialLoading = computed(
    () => !this.facade.membershipLoaded() || (this.loading() && !this.loaded()),
  );

  /**
   * Quote every row as it appears. In an effect rather than in `initialize()`
   * because the list is also written optimistically after an edit, and a row
   * that changed shape needs its figure again.
   */
  private readonly quoteEffect = effect(() => {
    for (const template of this.templates()) {
      this.facade.quoteTemplate(template);
    }
  });

  ngOnInit(): void {
    this.facade.initialize();
  }

  editRouteFor(template: RecurringBookingTemplateDto): string[] {
    return ['/' + CleansiaCustomerRoute.MEMBERSHIP, 'recurring', template.id ?? ''];
  }

  /**
   * What the schedule cleans, and how much of it — "Home cleaning · 3 rooms".
   * Falls back to the room count alone while the catalogue is still loading,
   * because a card with no heading reads as a broken row.
   */
  templateTitle(template: RecurringBookingTemplateDto): string {
    const names = [
      ...(template.selectedPackageIds ?? []).map((id) => this.facade.packageName(id)),
      ...(template.selectedServiceIds ?? []).map((id) => this.facade.serviceName(id)),
    ].filter((n): n is string => !!n);

    const rooms = this.roomsLabel(template.rooms);
    return names.length > 0 ? `${names.join(', ')} · ${rooms}` : rooms;
  }

  /**
   * "3 rooms" in the reader's language and their plural rules.
   *
   * `Intl.PluralRules` picks the category and the key carries it: Czech and
   * Slovak need one/few/many, Russian and Ukrainian the same three, English
   * only one/other. ngx-translate has no plural form of its own, and "1 rooms"
   * is the kind of wrong that makes the whole card look machine-made.
   */
  private roomsLabel(count: number): string {
    let category = 'other';
    try {
      category = new Intl.PluralRules(this.locale()).select(count);
    } catch {
      // A runtime without the locale data falls back to the plural form.
    }
    const key = `recurring_booking.rooms_count_${category}`;
    const translated = this.translate.instant(key, { count });
    return translated === key
      ? this.translate.instant('recurring_booking.rooms_count_other', { count })
      : translated;
  }

  /** "Wednesday 10:00 · next 3 Sep" — the second half only once we have one. */
  whenLine(template: RecurringBookingTemplateDto): string {
    const base = `${this.dayName(template.dayOfWeek)} ${template.timeOfDay ?? ''}`.trim();
    const next = this.facade.nextRun(template);
    // A paused schedule has no next run to promise, whatever the maths says.
    if (!next || !template.isActive) return base;
    return `${base} · ${this.translate.instant('recurring_booking.next_on', {
      date: next.toLocaleDateString(this.locale(), { day: 'numeric', month: 'long' }),
    })}`;
  }

  priceFor(template: RecurringBookingTemplateDto): string | null {
    const quoted = template.id ? this.facade.templatePrices()[template.id] : undefined;
    if (!quoted) return null;
    return new Intl.NumberFormat(this.locale(), {
      style: 'currency',
      currency: quoted.currency,
      minimumFractionDigits: 0,
    }).format(quoted.amount);
  }

  cadenceKey(frequency: number): string {
    switch (frequency as RecurrenceFrequency) {
      case RecurrenceFrequency.Biweekly:
        return 'recurring_booking.cadence_biweekly';
      case RecurrenceFrequency.Monthly:
        return 'recurring_booking.cadence_monthly';
      default:
        return 'recurring_booking.cadence_weekly';
    }
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

  private dayName(dotNetDow: number): string {
    // .NET DayOfWeek: Sun=0 … Sat=6. Pick a known Sunday (2024-01-07) and
    // offset, so the name comes from the runtime's own locale data rather than
    // seven more translation keys.
    const sunday = new Date('2024-01-07T12:00:00Z');
    const target = new Date(sunday);
    target.setUTCDate(sunday.getUTCDate() + dotNetDow);
    return target.toLocaleDateString(this.locale(), { weekday: 'long' });
  }
}
