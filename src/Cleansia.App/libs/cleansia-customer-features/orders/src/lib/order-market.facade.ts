import { inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { selectMarkets } from '@cleansia/customer-stores';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class OrderMarketFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly markets = toSignal(this.store.select(selectMarkets), { initialValue: [] });
  private readonly language = signal(this.translate.currentLang || this.translate.getDefaultLang());

  constructor() {
    super();
    this.translate.onLangChange.pipe(takeUntil(this.destroyed$))
      .subscribe(({ lang }) => this.language.set(lang));
  }

  label(order: { countryId?: string; currency?: { code?: string } } | null): string {
    const language = this.language();
    const market = order?.countryId
      ? this.markets().find((candidate) => candidate.countryId === order.countryId)
      : undefined;
    const country = market?.translations?.[language]?.name || market?.name
      || this.translate.instant('pages.orders.market_unknown');
    return this.translate.instant('pages.orders.market_label', {
      country,
      currency: order?.currency?.code || '—',
    });
  }
}
