import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { selectMarketCurrencyCode } from '@cleansia/customer-stores';
import { Store } from '@ngrx/store';
import { LegalDocumentComponent } from '../legal-document/legal-document.component';

/** The section stating what currency prices are displayed in. */
const ORDERING_SECTION = 3;

/**
 * The terms. Section 3 names the currency prices are displayed in, which is the market's
 * (ADR-0060 D3) — interpolated, never written into a locale file. With no market resolved the
 * paragraph states the market-agnostic sentence instead of an empty placeholder.
 */
@Component({
  selector: 'cleansia-customer-terms',
  standalone: true,
  imports: [LegalDocumentComponent],
  templateUrl: './terms.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsComponent {
  private readonly store = inject(Store);
  private readonly currencyCode = toSignal(this.store.select(selectMarketCurrencyCode), {
    initialValue: null,
  });

  sections = [1, 2, 3, 4, 5, 6];

  readonly sectionParams = computed<Record<number, Record<string, unknown>>>(() => {
    const currency = this.currencyCode();
    const params: Record<number, Record<string, unknown>> = {};
    if (currency) params[ORDERING_SECTION] = { currency };
    return params;
  });

  readonly sectionTextKeys = computed<Record<number, string>>(() => {
    const keys: Record<number, string> = {};
    if (!this.currencyCode()) keys[ORDERING_SECTION] = 'terms_page.section3_text_no_market';
    return keys;
  });
}
