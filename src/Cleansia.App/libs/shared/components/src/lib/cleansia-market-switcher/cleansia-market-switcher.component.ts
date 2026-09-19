import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { SelectModule } from 'primeng/select';

/** What one market has to carry to be listed; the customer wire's `MarketListItem` satisfies it. */
export interface CleansiaMarket {
  readonly isoCode: string | undefined;
  readonly isoAlpha2: string | undefined;
  readonly currencyCode: string | undefined;
  readonly name: string | undefined;
  readonly translations?: { [language: string]: { name?: string | undefined } } | undefined;
}

export interface CleansiaMarketOption {
  readonly value: string;
  /** "CZ · CZK" — what the closed pill and the chip print. */
  readonly short: string;
  /** "Česko · CZK" — what a row in the open list prints. */
  readonly label: string;
}

const SEPARATOR = ' · ';

/**
 * The market selector — the language switcher's twin (ADR-0058 D6).
 *
 * `pill` is the navbar/footer control and is drawn only with two or more markets. `chip` sits
 * beside the quick-quote amount as its unit: the same selector with two or more markets, a static
 * label with one, nothing with none. The country code is the glyph; there is no flag.
 *
 * Presentational: it emits the pick and persists nothing. The store owns the choice.
 */
@Component({
  selector: 'cleansia-market-switcher',
  templateUrl: './cleansia-market-switcher.component.html',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaMarketSwitcherComponent {
  readonly variant = input<'pill' | 'chip'>('pill');
  readonly markets = input.required<readonly CleansiaMarket[]>();
  readonly selected = input.required<string | null>();
  readonly marketChange = output<string>();

  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly lang = signal(this.translate.currentLang || this.translate.getDefaultLang());

  constructor() {
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ lang }) => this.lang.set(lang));
  }

  readonly options = computed<CleansiaMarketOption[]>(() => {
    const lang = this.lang();
    return this.markets()
      .filter((market): market is CleansiaMarket & { isoCode: string } => !!market.isoCode)
      .map((market) => ({
        value: market.isoCode,
        short: `${market.isoAlpha2 ?? market.isoCode}${SEPARATOR}${market.currencyCode ?? ''}`,
        label: `${market.translations?.[lang]?.name || market.name || market.isoCode}${SEPARATOR}${market.currencyCode ?? ''}`,
      }));
  });

  readonly hasChoice = computed(() => this.options().length >= 2);

  readonly current = computed(
    () => this.options().find((option) => option.value === this.selected()) ?? this.options()[0] ?? null,
  );

  onPick(isoCode: string): void {
    this.marketChange.emit(isoCode);
  }
}
