import { TemplateRef } from '@angular/core';
import { CurrencyListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

// Map currency codes to country codes for flag display
export const CURRENCY_TO_COUNTRY_MAP: Record<string, string> = {
  czk: 'cz',
  eur: 'eu',
  usd: 'us',
  gbp: 'gb',
  chf: 'ch',
  pln: 'pl',
  sek: 'se',
  nok: 'no',
  dkk: 'dk',
  huf: 'hu',
  ron: 'ro',
  bgn: 'bg',
  hrk: 'hr',
  rub: 'ru',
  uah: 'ua',
  jpy: 'jp',
  cny: 'cn',
  krw: 'kr',
  inr: 'in',
  brl: 'br',
  mxn: 'mx',
  cad: 'ca',
  aud: 'au',
  nzd: 'nz',
  sgd: 'sg',
  hkd: 'hk',
  thb: 'th',
  myr: 'my',
  idr: 'id',
  php: 'ph',
  vnd: 'vn',
  try: 'tr',
  zar: 'za',
  aed: 'ae',
  sar: 'sa',
  ils: 'il',
  egp: 'eg',
};

export function getCurrencyFlagCode(currencyCode: string | undefined): string {
  if (!currencyCode) return '';
  const lowerCode = currencyCode.toLowerCase();
  return CURRENCY_TO_COUNTRY_MAP[lowerCode] || '';
}

export const CURRENCY_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'currency.not_found': 'errors.currency.not_found',
  'currency.in_use': 'errors.currency.in_use',
  'currency.cannot_delete_default': 'errors.currency.cannot_delete_default',
};

export const CURRENCY_FALLBACK_ERROR_KEY = 'errors.common.error_occurred';

export function resolveCurrencyErrorKey(error: unknown): string {
  const apiError = error as {
    result?: { detail?: string; title?: string };
    response?: string;
  };
  let code = apiError?.result?.detail || apiError?.result?.title;

  if (!code && apiError?.response) {
    try {
      const parsed = JSON.parse(apiError.response) as {
        detail?: string;
        title?: string;
      };
      code = parsed.detail || parsed.title;
    } catch {
      code = undefined;
    }
  }

  if (code && CURRENCY_ERROR_KEY_MAP[code]) {
    return CURRENCY_ERROR_KEY_MAP[code];
  }
  return CURRENCY_FALLBACK_ERROR_KEY;
}

export function getCurrencyTableDefinition(
  defs: {
    onEdit: (row: CurrencyListItem) => void;
    onDelete: (row: CurrencyListItem) => void;
    onSetDefault: (row: CurrencyListItem) => void;
  },
  translate: TranslateService,
  flagTemplate?: TemplateRef<CurrencyListItem>
): { columns: TableColumn<CurrencyListItem>[]; actions: TableAction<CurrencyListItem>[] } {
  return {
    columns: [
      {
        id: 'flag',
        field: 'code',
        header: '',
        sortable: false,
        width: '60px',
        customTemplate: flagTemplate,
      },
      {
        id: 'code',
        field: 'code',
        header: translate.instant('pages.currency_management.columns.code'),
        sortable: true,
        width: '12%',
      },
      {
        id: 'symbol',
        field: 'symbol',
        header: translate.instant('pages.currency_management.columns.symbol'),
        width: '10%',
      },
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.currency_management.columns.name'),
        sortable: true,
        width: '30%',
      },
      {
        id: 'exchangeRate',
        field: 'exchangeRate',
        header: translate.instant('pages.currency_management.columns.exchange_rate'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'isDefault',
        field: 'isDefault',
        header: translate.instant('pages.currency_management.columns.is_default'),
        getValue: (row: CurrencyListItem) =>
          row.isDefault
            ? translate.instant('global.yes')
            : translate.instant('global.no'),
        width: '10%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.currency_management.edit_currency'),
        color: 'warning',
        onClick: (row: CurrencyListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-star',
        tooltip: translate.instant('pages.currency_management.set_default'),
        color: 'info',
        onClick: (row: CurrencyListItem) => defs.onSetDefault(row),
        visible: (row: CurrencyListItem) => !row.isDefault,
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.currency_management.delete_currency'),
        color: 'danger',
        onClick: (row: CurrencyListItem) => defs.onDelete(row),
        visible: (row: CurrencyListItem) => !row.isDefault,
      },
    ],
  };
}