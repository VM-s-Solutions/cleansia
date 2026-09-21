import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Translation } from 'primeng/api';
import { PrimeNG } from 'primeng/config';

const DAYS = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'];
const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];

export function buildPrimeNgTranslation(translate: TranslateService): Translation {
  const word = (key: string): string => translate.instant(`primeng.${key}`);
  const words = (group: string, keys: string[]): string[] => keys.map((key) => word(`${group}.${key}`));

  return {
    firstDayOfWeek: 1,
    dayNames: words('day_names', DAYS),
    dayNamesShort: words('day_names_short', DAYS),
    dayNamesMin: words('day_names_min', DAYS),
    monthNames: words('month_names', MONTHS),
    monthNamesShort: words('month_names_short', MONTHS),
    today: word('today'),
    clear: word('clear'),
    apply: word('apply'),
    accept: word('accept'),
    reject: word('reject'),
    emptyMessage: word('empty_message'),
    emptyFilterMessage: word('empty_filter_message'),
  };
}

/**
 * PrimeNG's calendar, select and confirm-dialog words come from the same bundle as everything else.
 * The bundle arrives after bootstrap, so the first apply waits for `onLangChange` unless the
 * translations are already there; each language switch re-applies.
 */
export function providePrimeNgTranslation(): EnvironmentProviders {
  return provideAppInitializer(() => {
    const translate = inject(TranslateService);
    const primeng = inject(PrimeNG);
    const apply = () => primeng.setTranslation(buildPrimeNgTranslation(translate));

    if (translate.instant('primeng.today') !== 'primeng.today') {
      apply();
    }
    translate.onLangChange.subscribe(apply);
  });
}
