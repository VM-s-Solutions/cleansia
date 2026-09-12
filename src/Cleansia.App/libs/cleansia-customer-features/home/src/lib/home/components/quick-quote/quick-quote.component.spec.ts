import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY } from 'rxjs';
import { QuickQuoteComponent } from './quick-quote.component';
import { QuickQuoteFacade } from './quick-quote.facade';

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
