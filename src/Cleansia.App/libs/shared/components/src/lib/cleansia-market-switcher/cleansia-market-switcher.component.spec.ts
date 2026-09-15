import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import {
  CleansiaMarket,
  CleansiaMarketSwitcherComponent,
} from './cleansia-market-switcher.component';

const CZE: CleansiaMarket = {
  isoCode: 'CZE',
  isoAlpha2: 'CZ',
  currencyCode: 'CZK',
  name: 'Czechia',
  translations: { cs: { name: 'Česko' }, en: { name: 'Czechia' } },
};
const SVK: CleansiaMarket = {
  isoCode: 'SVK',
  isoAlpha2: 'SK',
  currencyCode: 'EUR',
  name: 'Slovakia',
  translations: { cs: { name: 'Slovensko' } },
};

@Component({
  standalone: true,
  imports: [CleansiaMarketSwitcherComponent],
  template: `
    <cleansia-market-switcher
      [variant]="variant()"
      [markets]="markets()"
      [selected]="selected()"
      (marketChange)="picked = $event"
    />
  `,
})
class HostComponent {
  readonly variant = signal<'pill' | 'chip'>('pill');
  readonly markets = signal<readonly CleansiaMarket[]>([CZE, SVK]);
  readonly selected = signal<string | null>('CZE');
  picked: string | null = null;
}

describe('CleansiaMarketSwitcherComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let translate: TranslateService;

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const text = (): string => element().textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const component = (): CleansiaMarketSwitcherComponent =>
    fixture.debugElement.children[0].componentInstance as CleansiaMarketSwitcherComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent, TranslateModule.forRoot()],
    }).compileComponents();
    translate = TestBed.inject(TranslateService);
    translate.use('en');
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    // Two passes: ngModel writes the selected value to the select after the first.
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('labels the pill with the alpha-2 code and the currency code, and draws no flag', () => {
    expect(text()).toContain('CZ · CZK');
    expect(element().querySelector('.fi')).toBeNull();
    expect(element().querySelector('.pi-map')).not.toBeNull();
  });

  it('lists every market by its translated name with the currency code', () => {
    translate.use('cs');
    fixture.detectChanges();

    expect(component().options().map((o) => o.label)).toEqual(['Česko · CZK', 'Slovensko · EUR']);
  });

  it('falls back to the wire name when no translation exists for the language', () => {
    translate.use('uk');
    fixture.detectChanges();

    expect(component().options().map((o) => o.label)).toEqual(['Czechia · CZK', 'Slovakia · EUR']);
  });

  it('emits the picked code and persists nothing itself', () => {
    component().onPick('SVK');

    expect(host.picked).toBe('SVK');
    expect(document.cookie).not.toContain('preferred_market');
  });

  // The pill is a control with something to choose between and nothing at all otherwise: with one
  // market today the navbar and footer are pixel-identical to the tree before this component.
  it('draws no pill with fewer than two markets', () => {
    host.markets.set([CZE]);
    fixture.detectChanges();

    expect(element().querySelector('.cleansia-market-switcher')).toBeNull();
    expect(text()).toBe('');
  });

  describe('chip variant', () => {
    beforeEach(() => {
      host.variant.set('chip');
      fixture.detectChanges();
    });

    it('is a control that opens the same selector with two or more markets', () => {
      expect(element().querySelector('p-select')).not.toBeNull();
      expect(element().querySelector('.cl-chip--static')).toBeNull();
      expect(text()).toContain('CZ · CZK');
    });

    it('is a static label, not a control, with one market', () => {
      host.markets.set([CZE]);
      fixture.detectChanges();

      const chip = element().querySelector('.cl-chip--static');
      expect(chip?.tagName).toBe('SPAN');
      expect(chip?.textContent?.trim()).toBe('CZ · CZK');
      expect(chip?.getAttribute('tabindex')).toBeNull();
      expect(element().querySelector('p-select')).toBeNull();
    });

    it('draws nothing with no market', () => {
      host.markets.set([]);
      fixture.detectChanges();

      expect(text()).toBe('');
    });
  });
});
