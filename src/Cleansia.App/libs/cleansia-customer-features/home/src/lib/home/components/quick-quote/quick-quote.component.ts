import { ChangeDetectionStrategy, Component, inject, input, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

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
  imports: [TranslatePipe, RouterModule],
  providers: [QuickQuoteFacade],
})
export class QuickQuoteComponent implements OnInit {
  readonly services = input<readonly QuickQuoteService[]>([]);

  readonly facade = inject(QuickQuoteFacade);

  ngOnInit(): void {
    const first = this.services()[0];
    if (first) {
      this.facade.selectService(first.id);
    }
  }

  /** No cleaning can be booked for yesterday. */
  readonly today = new Date().toISOString().slice(0, 10);

  onTime(event: Event): void {
    this.facade.selectTime((event.target as HTMLInputElement).value);
  }

  onDate(event: Event): void {
    this.facade.selectDate((event.target as HTMLInputElement).value);
  }

  onSize(size: PropertySizePreset): void {
    this.facade.selectSize(size);
  }
}
