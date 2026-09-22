import { TemplateRef } from '@angular/core';
import { CountryListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

// Map ISO 3166-1 alpha-3 codes to alpha-2 codes for flag-icons
// The database stores 3-letter codes (MaxLength(3)), flag-icons uses 2-letter codes
export const ISO_ALPHA3_TO_ALPHA2: Record<string, string> = {
  cze: 'cz',
  usa: 'us',
  gbr: 'gb',
  deu: 'de',
  fra: 'fr',
  esp: 'es',
  ita: 'it',
  pol: 'pl',
  svk: 'sk',
  aut: 'at',
  che: 'ch',
  nld: 'nl',
  bel: 'be',
  dnk: 'dk',
  swe: 'se',
  nor: 'no',
  fin: 'fi',
  hun: 'hu',
  rou: 'ro',
  bgr: 'bg',
  hrv: 'hr',
  svn: 'si',
  srb: 'rs',
  ukr: 'ua',
  rus: 'ru',
  blr: 'by',
  ltu: 'lt',
  lva: 'lv',
  est: 'ee',
  prt: 'pt',
  grc: 'gr',
  irl: 'ie',
  lux: 'lu',
  mlt: 'mt',
  cyp: 'cy',
  tur: 'tr',
  isr: 'il',
  jpn: 'jp',
  chn: 'cn',
  kor: 'kr',
  ind: 'in',
  aus: 'au',
  nzl: 'nz',
  can: 'ca',
  mex: 'mx',
  bra: 'br',
  arg: 'ar',
  chl: 'cl',
  col: 'co',
  zaf: 'za',
  egy: 'eg',
  sau: 'sa',
  are: 'ae',
  sgp: 'sg',
  hkg: 'hk',
  tha: 'th',
  mys: 'my',
  idn: 'id',
  phl: 'ph',
  vnm: 'vn',
};

// Map for 2-letter codes that need special handling
export const ISO_ALPHA2_SPECIAL: Record<string, string> = {
  uk: 'gb',
};

export function getCountryFlagCode(isoCode: string | undefined): string {
  if (!isoCode) return '';
  const lowerCode = isoCode.toLowerCase();

  // First check if it's a 3-letter code
  if (lowerCode.length === 3 && ISO_ALPHA3_TO_ALPHA2[lowerCode]) {
    return ISO_ALPHA3_TO_ALPHA2[lowerCode];
  }

  // Then check for 2-letter special cases
  if (ISO_ALPHA2_SPECIAL[lowerCode]) {
    return ISO_ALPHA2_SPECIAL[lowerCode];
  }

  // For 2-letter codes, use as-is (most work directly)
  return lowerCode;
}

export function getCountryTableDefinition(
  defs: {
    onEdit: (row: CountryListItem) => void;
    onDelete: (row: CountryListItem) => void;
    onSetDefaultMarket: (row: CountryListItem) => void;
  },
  translate: TranslateService,
  permissions: PermissionService,
  flagTemplate?: TemplateRef<CountryListItem>
): { columns: TableColumn<CountryListItem>[]; actions: TableAction<CountryListItem>[] } {
  return {
    columns: [
      {
        id: 'flag',
        field: 'isoCode',
        header: '',
        sortable: false,
        width: '60px',
        customTemplate: flagTemplate,
      },
      {
        id: 'isoCode',
        field: 'isoCode',
        header: translate.instant('pages.country_management.columns.iso_code'),
        sortable: true,
        width: '15%',
      },
      {
        id: 'name',
        field: 'name',
        header: translate.instant('pages.country_management.columns.name'),
        sortable: true,
        width: '40%',
      },
      {
        id: 'isDefaultMarket',
        field: 'isDefaultMarket',
        header: translate.instant('pages.country_management.columns.default_market'),
        sortable: false,
        width: '15%',
        getValue: (row: CountryListItem) => (row.isDefaultMarket ? translate.instant('global.yes') : '—'),
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.country_management.edit_country'),
        color: 'warning',
        visible: () => permissions.hasPolicy(Policy.CanUpdateCountry),
        onClick: (row: CountryListItem) => defs.onEdit(row),
      },
      {
        // The list item does not say whether the country is serviced, so the star is offered on
        // every non-default row and the server's country.not_serviced refusal surfaces as a toast.
        icon: 'pi pi-star',
        tooltip: translate.instant('pages.country_management.set_default_market'),
        color: 'info',
        onClick: (row: CountryListItem) => defs.onSetDefaultMarket(row),
        visible: (row: CountryListItem) =>
          !row.isDefaultMarket && permissions.hasPolicy(Policy.CanUpdateCountry),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.country_management.delete_country'),
        color: 'danger',
        visible: () => permissions.hasPolicy(Policy.CanDeleteCountry),
        onClick: (row: CountryListItem) => defs.onDelete(row),
      },
    ],
  };
}