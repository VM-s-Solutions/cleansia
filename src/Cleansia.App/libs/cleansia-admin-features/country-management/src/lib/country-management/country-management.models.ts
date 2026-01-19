import { TemplateRef } from '@angular/core';
import { CountryListItem } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
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
  },
  translate: TranslateService,
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
        width: '55%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-pencil',
        tooltip: translate.instant('pages.country_management.edit_country'),
        color: 'warning',
        onClick: (row: CountryListItem) => defs.onEdit(row),
      },
      {
        icon: 'pi pi-trash',
        tooltip: translate.instant('pages.country_management.delete_country'),
        color: 'danger',
        onClick: (row: CountryListItem) => defs.onDelete(row),
      },
    ],
  };
}