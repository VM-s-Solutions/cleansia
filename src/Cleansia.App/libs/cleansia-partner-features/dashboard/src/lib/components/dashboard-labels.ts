import { toSnakeCase } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export function orderStatusLegendLabel(translate: TranslateService, status: string): string {
  const key = `enums.order_status.${toSnakeCase(status)}`;
  const translated = translate.instant(key);
  return translated === key ? status : translated;
}

export function monthName(year: number, month: number, lang: string): string {
  return new Intl.DateTimeFormat(lang, { month: 'long', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, 1))
  );
}
