import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { selectMarketCurrencyCode, selectMarketNoShowCredit } from '@cleansia/customer-stores';
import { formatMoney, localeFor } from '@cleansia/utils';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CleansiaTitleComponent } from '@cleansia/components/cleansia-title';

/**
 * The operating rules, stated on the page rather than buried in the terms.
 *
 * The hour and percentage figures are pinned to `BookingPolicy` by the parity
 * checker. The one money figure — the apology credit when we cancel — is
 * authored per currency (`Currency.NoShowCredit`) and arrives with the market,
 * so the copy carries a placeholder and the amount is formatted here in the
 * market's currency; a market with no credit gets the refund promise alone.
 * This section replaces the testimonials block, which rendered three invented
 * customers and a hardcoded five-star row against a system that aggregates no
 * ratings. -> /product/business-rules#money-constants
 */
@Component({
  selector: 'cleansia-rules',
  templateUrl: './rules.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, CleansiaTitleComponent],
})
export class RulesComponent {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly noShowCredit = toSignal(this.store.select(selectMarketNoShowCredit), {
    initialValue: null,
  });
  private readonly currencyCode = toSignal(this.store.select(selectMarketCurrencyCode), {
    initialValue: null,
  });
  private readonly lang = signal(this.translate.currentLang);

  constructor() {
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ lang }) => this.lang.set(lang));
  }

  /** The credit as money in the market's currency, or null when the market authors none. */
  readonly creditAmount = computed(() => {
    const credit = this.noShowCredit();
    const currencyCode = this.currencyCode();
    if (credit === null || credit <= 0 || !currencyCode) return null;
    return formatMoney(credit, currencyCode, localeFor(this.lang()));
  });
}
