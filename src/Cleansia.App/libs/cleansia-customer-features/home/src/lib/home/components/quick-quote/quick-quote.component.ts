import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { EXPRESS_LEAD_TIME_HOURS, generateTimeOptions } from '@cleansia/models';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';

import { QuickQuoteFacade } from './quick-quote.facade';
import { PropertySizePreset } from './property-size-presets';

/** A service the visitor can price, as the home page already loads them. */
export interface QuickQuoteService {
  readonly id: string;
  readonly name: string;
}

/**
 * The price calculator that sits in the hero.
 *
 * Presentational only — every decision lives in `QuickQuoteFacade`. The point of
 * putting it above the fold is that "fixed price" stops being a claim and
 * becomes a number the visitor watched the server produce.
 */
@Component({
  selector: 'cleansia-quick-quote',
  templateUrl: './quick-quote.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, RouterModule, FormsModule, DatePickerModule, SelectModule],
  providers: [QuickQuoteFacade],
})
export class QuickQuoteComponent {
  readonly services = input<readonly QuickQuoteService[]>([]);

  readonly facade = inject(QuickQuoteFacade);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ lang }) => this.lang.set(lang));

    // An effect, not ngOnInit: the catalogue is fetched, so on first init
    // `services()` is still empty and `services()[0]` is undefined. Nothing was
    // ever selected, the card sat on "pick a service" and the calculator could
    // not produce a price until the visitor clicked one — while the artboard
    // shows a chip already chosen and a number already on screen.
    effect(() => {
      const first = this.services()[0];
      if (first && !this.facade.selectedServiceId()) {
        this.facade.selectService(first.id);
      }
    });
  }

  /**
   * Widths of the placeholder chips shown until the catalogue resolves. Five,
   * because `quoteServices` caps at five, and uneven so the row reads as
   * loading content rather than as a control someone forgot to fill.
   */
  readonly placeholderChipWidths = [104, 132, 88, 116, 76];

  /**
   * No cleaning can be booked for yesterday.
   *
   * Midnight, not `new Date()`. The picker compares the value it produces —
   * which is always 00:00 on the chosen day — against `minDate`, so a `minDate`
   * carrying the current time made TODAY fail its own minimum: tapping today
   * cleared the field instead of selecting it, from 00:01 onwards.
   */
  readonly today = (() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
  })();

  /** The facade keeps the date as `YYYY-MM-DD`; the picker wants a Date. */
  readonly selectedDate = computed<Date | null>(() => {
    const iso = this.facade.cleaningDate();
    return iso ? new Date(`${iso}T00:00:00`) : null;
  });

  /**
   * The same 08:00-20:00 window the booking wizard offers, from the one shared
   * definition — a visitor must not be quoted an hour the wizard then refuses.
   * Slots inside the express lead time are disabled rather than hidden, so the
   * list does not shrink as the day runs out. The express surcharge itself is
   * the wizard's to explain — this control only refuses what cannot be booked.
   */
  readonly timeOptions = computed(() => {
    const date = this.selectedDate();
    const now = new Date();
    const isToday = !!date && date.toDateString() === now.toDateString();

    return generateTimeOptions().map((opt) => {
      if (!isToday || !date) return { ...opt, disabled: false };
      const [hour, minute] = opt.value.split(':').map(Number);
      const slot = new Date(date);
      slot.setHours(hour, minute, 0, 0);
      const hoursAhead = (slot.getTime() - now.getTime()) / (60 * 60 * 1000);
      return { ...opt, disabled: hoursAhead < EXPRESS_LEAD_TIME_HOURS };
    });
  });

  /**
   * Bumped on every language change. `translate.currentLang` is a plain
   * property, so a computed that reads it never re-runs — the decimal separator
   * and the plural form both froze in whatever language the page loaded in.
   */
  private readonly lang = signal(this.translate.currentLang);

  /**
   * Minutes rounded to the nearest half hour, as the artboard states it, and
   * formatted for the active locale — Czech writes a half hour as "2,5", not
   * "2.5", and the raw number went straight into the string.
   */
  readonly crewHours = computed(() => {
    const mins = this.facade.crewMinutes();
    if (!mins) return null;
    const hours = Math.round((mins / 60) * 2) / 2;
    return new Intl.NumberFormat(this.localeTag(), { maximumFractionDigits: 1 }).format(hours);
  });

  private localeTag(): string {
    return this.lang() || this.translate.getDefaultLang() || 'cs';
  }

  onDate(value: Date | null): void {
    if (!value) {
      this.facade.selectDate('');
      return;
    }
    // Local parts, not toISOString() — that shifts to UTC and can hand the
    // backend yesterday for anyone east of Greenwich.
    const pad = (n: number) => n.toString().padStart(2, '0');
    this.facade.selectDate(`${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`);
  }

  onSize(size: PropertySizePreset): void {
    this.facade.selectSize(size);
  }
}
