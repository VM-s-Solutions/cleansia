import { TemplateRef } from '@angular/core';
import { AdminCurrencyListItem } from '@cleansia/admin-services';
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
  'currency.not_found': 'api.currency.not_found',
  'currency.in_use': 'api.currency.in_use',
  'currency.cannot_delete_default': 'api.currency.cannot_delete_default',
  'currency.cannot_deactivate_default': 'api.currency.cannot_deactivate_default',
  'currency.invalid': 'api.currency.invalid',
  'currency.not_priced': 'api.currency.not_priced',
};

export const CURRENCY_FALLBACK_ERROR_KEY = 'api.common.error_occurred';

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
    onEdit: (row: AdminCurrencyListItem) => void;
    onDelete: (row: AdminCurrencyListItem) => void;
    onSetDefault: (row: AdminCurrencyListItem) => void;
    onDeactivate: (row: AdminCurrencyListItem) => void;
    onActivate: (row: AdminCurrencyListItem) => void;
  },
  translate: TranslateService,
  flagTemplate?: TemplateRef<AdminCurrencyListItem>
): { columns: TableColumn<AdminCurrencyListItem>[]; actions: TableAction<AdminCurrencyListItem>[] } {
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
        width: '32%',
      },
      {
        // WHETHER THE PLATFORM SELLS IN IT. Without this column an admin cannot tell why the star
        // refuses a currency (SetDefaultCurrency will not promote an inactive one) or why the
        // catalogue form insists on pricing some currencies and not others.
        id: 'isActive',
        field: 'isActive',
        header: translate.instant('pages.currency_management.columns.is_active'),
        getValue: (row: AdminCurrencyListItem) =>
          row.isActive
            ? translate.instant('pages.currency_management.operated')
            : translate.instant('pages.currency_management.not_operated'),
        width: '13%',
      },
      {
        id: 'isDefault',
        field: 'isDefault',
        header: translate.instant('pages.currency_management.columns.is_default'),
        getValue: (row: AdminCurrencyListItem) =>
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
        onClick: (row: AdminCurrencyListItem) => defs.onEdit(row),
      },
      {
        // Hidden on a row the server would refuse: SetDefaultCurrency will not promote a currency
        // the platform does not sell in, so the star is offered only once the row is switched on.
        icon: 'pi pi-star',
        tooltip: translate.instant('pages.currency_management.set_default'),
        color: 'info',
        onClick: (row: AdminCurrencyListItem) => defs.onSetDefault(row),
        visible: (row: AdminCurrencyListItem) => !row.isDefault && row.isActive,
      },
      {
        // THE MARKET SWITCH. Off is hidden on the default row for the same reason delete is: the
        // server refuses it, and an admin should not be offered a button that only ever says no.
        icon: 'pi pi-ban',
        tooltip: translate.instant('pages.currency_management.deactivate'),
        color: 'danger',
        visible: (row: AdminCurrencyListItem) => row.isActive && !row.isDefault,
        onClick: (row: AdminCurrencyListItem) => defs.onDeactivate(row),
      },
      {
        icon: 'pi pi-check-circle',
        tooltip: translate.instant('pages.currency_management.activate'),
        color: 'success',
        visible: (row: AdminCurrencyListItem) => !row.isActive,
        onClick: (row: AdminCurrencyListItem) => defs.onActivate(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.currency_management.delete_currency'),
        color: 'danger',
        onClick: (row: AdminCurrencyListItem) => defs.onDelete(row),
        visible: (row: AdminCurrencyListItem) => !row.isDefault,
      },
    ],
  };
}