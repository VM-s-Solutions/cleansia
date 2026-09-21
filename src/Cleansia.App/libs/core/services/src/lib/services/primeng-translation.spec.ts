import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LangChangeEvent, TranslateService } from '@ngx-translate/core';
import { PrimeNG } from 'primeng/config';
import { Subject } from 'rxjs';
import { buildPrimeNgTranslation, providePrimeNgTranslation } from './primeng-translation';

const bundle: Record<string, string> = {
  'primeng.day_names.sun': 'Neděle',
  'primeng.day_names.mon': 'Pondělí',
  'primeng.day_names.tue': 'Úterý',
  'primeng.day_names.wed': 'Středa',
  'primeng.day_names.thu': 'Čtvrtek',
  'primeng.day_names.fri': 'Pátek',
  'primeng.day_names.sat': 'Sobota',
  'primeng.day_names_short.sun': 'Ne',
  'primeng.day_names_short.mon': 'Po',
  'primeng.day_names_short.tue': 'Út',
  'primeng.day_names_short.wed': 'St',
  'primeng.day_names_short.thu': 'Čt',
  'primeng.day_names_short.fri': 'Pá',
  'primeng.day_names_short.sat': 'So',
  'primeng.day_names_min.sun': 'N',
  'primeng.day_names_min.mon': 'P',
  'primeng.day_names_min.tue': 'Ú',
  'primeng.day_names_min.wed': 'S',
  'primeng.day_names_min.thu': 'Č',
  'primeng.day_names_min.fri': 'P',
  'primeng.day_names_min.sat': 'S',
  'primeng.month_names.jan': 'Leden',
  'primeng.month_names.feb': 'Únor',
  'primeng.month_names.mar': 'Březen',
  'primeng.month_names.apr': 'Duben',
  'primeng.month_names.may': 'Květen',
  'primeng.month_names.jun': 'Červen',
  'primeng.month_names.jul': 'Červenec',
  'primeng.month_names.aug': 'Srpen',
  'primeng.month_names.sep': 'Září',
  'primeng.month_names.oct': 'Říjen',
  'primeng.month_names.nov': 'Listopad',
  'primeng.month_names.dec': 'Prosinec',
  'primeng.month_names_short.jan': 'Led',
  'primeng.month_names_short.feb': 'Úno',
  'primeng.month_names_short.mar': 'Bře',
  'primeng.month_names_short.apr': 'Dub',
  'primeng.month_names_short.may': 'Kvě',
  'primeng.month_names_short.jun': 'Čvn',
  'primeng.month_names_short.jul': 'Čvc',
  'primeng.month_names_short.aug': 'Srp',
  'primeng.month_names_short.sep': 'Zář',
  'primeng.month_names_short.oct': 'Říj',
  'primeng.month_names_short.nov': 'Lis',
  'primeng.month_names_short.dec': 'Pro',
  'primeng.today': 'Dnes',
  'primeng.clear': 'Vymazat',
  'primeng.apply': 'Použít',
  'primeng.accept': 'Ano',
  'primeng.reject': 'Ne',
  'primeng.empty_message': 'Nic nenalezeno',
  'primeng.empty_filter_message': 'Nic nenalezeno',
};

function translateStub(loaded: boolean) {
  const onLangChange = new Subject<LangChangeEvent>();
  return {
    onLangChange,
    stub: {
      onLangChange,
      instant: (key: string) => (loaded ? bundle[key] ?? key : key),
    },
  };
}

describe('buildPrimeNgTranslation', () => {
  it('reads every calendar, select and confirm word from the primeng.* bundle, Monday first', () => {
    const { stub } = translateStub(true);

    const translation = buildPrimeNgTranslation(stub as unknown as TranslateService);

    expect(translation.firstDayOfWeek).toBe(1);
    expect(translation.dayNames).toEqual(['Neděle', 'Pondělí', 'Úterý', 'Středa', 'Čtvrtek', 'Pátek', 'Sobota']);
    expect(translation.dayNamesShort).toEqual(['Ne', 'Po', 'Út', 'St', 'Čt', 'Pá', 'So']);
    expect(translation.dayNamesMin).toEqual(['N', 'P', 'Ú', 'S', 'Č', 'P', 'S']);
    expect(translation.monthNames?.[0]).toBe('Leden');
    expect(translation.monthNames?.[11]).toBe('Prosinec');
    expect(translation.monthNamesShort?.[8]).toBe('Zář');
    expect(translation.today).toBe('Dnes');
    expect(translation.clear).toBe('Vymazat');
    expect(translation.apply).toBe('Použít');
    expect(translation.accept).toBe('Ano');
    expect(translation.reject).toBe('Ne');
    expect(translation.emptyMessage).toBe('Nic nenalezeno');
    expect(translation.emptyFilterMessage).toBe('Nic nenalezeno');
  });
});

describe('providePrimeNgTranslation', () => {
  it('applies the bundle once the language has loaded and again on every language change', async () => {
    const { stub, onLangChange } = translateStub(false);
    const setTranslation = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        providePrimeNgTranslation(),
        { provide: TranslateService, useValue: stub },
        { provide: PrimeNG, useValue: { setTranslation } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;

    expect(setTranslation).not.toHaveBeenCalled();

    stub.instant = (key: string) => bundle[key] ?? key;
    onLangChange.next({ lang: 'cs', translations: {} });

    expect(setTranslation).toHaveBeenCalledTimes(1);
    expect(setTranslation.mock.calls[0][0].today).toBe('Dnes');

    onLangChange.next({ lang: 'cs', translations: {} });
    expect(setTranslation).toHaveBeenCalledTimes(2);
  });

  it('applies immediately when the bundle is already loaded at bootstrap', async () => {
    const { stub } = translateStub(true);
    const setTranslation = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        providePrimeNgTranslation(),
        { provide: TranslateService, useValue: stub },
        { provide: PrimeNG, useValue: { setTranslation } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;

    expect(setTranslation).toHaveBeenCalledTimes(1);
    expect(setTranslation.mock.calls[0][0].accept).toBe('Ano');
  });
});
