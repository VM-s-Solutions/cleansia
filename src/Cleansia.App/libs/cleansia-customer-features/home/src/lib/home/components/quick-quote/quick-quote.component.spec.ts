import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { DirtinessLevel } from '@cleansia/customer-services';
import { lastBookableDay } from '@cleansia/models';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DatePicker } from 'primeng/datepicker';
import { EMPTY } from 'rxjs';
import { PropertySizePreset } from './property-size-presets';
import { QuickQuoteComponent } from './quick-quote.component';
import { QuickQuoteFacade } from './quick-quote.facade';

function stubFacade(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    cleaningDate: signal<string | null>(null),
    cleaningTime: signal<string | null>(null),
    selectedServiceId: signal<string | null>(null),
    sizes: signal<PropertySizePreset[]>([]),
    showSizeRow: signal(false),
    dirtinessLevel: signal(DirtinessLevel.Normal),
    dirtinessSurcharge: signal(null),
    isLoading: signal(false),
    priceLabelKey: signal('pages.home.quote.pick_service'),
    amountLabel: signal<string | null>(null),
    markets: signal([]),
    selectedMarketCode: signal<string | null>(null),
    crewMinutes: signal<number | null>(null),
    continueQueryParams: signal({}),
    selectService: jest.fn(),
    selectDate: jest.fn(),
    selectTime: jest.fn(),
    selectDirtinessLevel: jest.fn(),
    chooseMarket: jest.fn(),
    ...overrides,
  };
}

async function render(facade: Record<string, unknown>) {
  await TestBed.configureTestingModule({
    imports: [QuickQuoteComponent, TranslateModule.forRoot()],
    providers: [provideRouter([]), provideNoopAnimations()],
  })
    .overrideComponent(QuickQuoteComponent, {
      set: { providers: [{ provide: QuickQuoteFacade, useValue: facade }] },
    })
    .compileComponents();
  const fixture = TestBed.createComponent(QuickQuoteComponent);
  fixture.detectChanges();
  return fixture;
}

describe('home calculator arrival times', () => {
  const cleaningDate = signal('2026-09-10');

  beforeEach(() => {
    jest.useFakeTimers().setSystemTime(new Date(2026, 8, 10, 10, 15));
    cleaningDate.set('2026-09-10');
    TestBed.configureTestingModule({
      providers: [
        {
          provide: QuickQuoteFacade,
          useValue: {
            cleaningDate,
            selectedServiceId: signal(null),
            selectService: jest.fn(),
          },
        },
        {
          provide: TranslateService,
          useValue: {
            currentLang: 'en',
            getDefaultLang: () => 'en',
            onLangChange: EMPTY,
          },
        },
      ],
    });
  });

  afterEach(() => jest.useRealTimers());

  it('enables quarter hours at and after the exact two-hour minimum', () => {
    const component = TestBed.runInInjectionContext(() => new QuickQuoteComponent());
    const options = component.timeOptions();

    expect(options.find((option) => option.value === '12:00')?.disabled).toBe(true);
    expect(options.find((option) => option.value === '12:15')?.disabled).toBe(false);
    expect(options.find((option) => option.value === '12:30')?.disabled).toBe(false);
    expect(options.find((option) => option.value === '12:45')?.disabled).toBe(false);
  });

  it('rejects a quarter hour one second inside the minimum lead time', () => {
    jest.setSystemTime(new Date(2026, 8, 10, 10, 15, 1));
    const component = TestBed.runInInjectionContext(() => new QuickQuoteComponent());
    const options = component.timeOptions();

    expect(options.find((option) => option.value === '12:15')?.disabled).toBe(true);
    expect(options.find((option) => option.value === '12:30')?.disabled).toBe(false);
  });

  it('offers all 48 quarter hours on a future date', () => {
    cleaningDate.set('2026-09-11');
    const component = TestBed.runInInjectionContext(() => new QuickQuoteComponent());
    const options = component.timeOptions();

    expect(options).toHaveLength(48);
    expect(options.every((option) => !option.disabled)).toBe(true);
  });
});

describe('home calculator dates', () => {
  const now = new Date(2026, 8, 10, 10, 15);

  afterEach(() => jest.useRealTimers());

  it('offers no day past the last one inside the booking horizon', async () => {
    jest.useFakeTimers().setSystemTime(now);

    const fixture = await render(stubFacade());
    const picker = fixture.debugElement.query(By.directive(DatePicker)).componentInstance as DatePicker;

    expect(picker.maxDate).toEqual(lastBookableDay(now));
  });
});

describe('home calculator size row', () => {
  const ladder: PropertySizePreset[] = [
    { code: 'CZ_1KK', label: '1+kk', rooms: 1, bathrooms: 1 },
    { code: 'CZ_2KK', label: '2+kk', rooms: 2, bathrooms: 1 },
  ];

  const sizeRow = (root: HTMLElement) => root.querySelector('.cl-quote__chips--sizes');

  it('holds the row with placeholder chips while the presets are in flight', async () => {
    const fixture = await render(stubFacade({ showSizeRow: signal(true) }));
    const row = sizeRow(fixture.nativeElement);

    const ghosts = row?.querySelectorAll('.cl-quote__chip--ghost[aria-hidden="true"]') ?? [];
    expect(ghosts).toHaveLength(5);
    expect(row?.querySelectorAll('button')).toHaveLength(0);
  });

  it('puts the market’s presets where the placeholders stood', async () => {
    const sizes = signal<PropertySizePreset[]>([]);
    const fixture = await render(
      stubFacade({ sizes, showSizeRow: signal(true), selectedSize: signal(ladder[0]) }),
    );

    sizes.set(ladder);
    fixture.detectChanges();
    const row = sizeRow(fixture.nativeElement);

    expect(row?.querySelectorAll('.cl-quote__chip--ghost')).toHaveLength(0);
    const chips = Array.from(row?.querySelectorAll('button') ?? []);
    expect(chips.map((chip) => chip.textContent?.trim())).toEqual(['1+kk', '2+kk']);
  });
});
