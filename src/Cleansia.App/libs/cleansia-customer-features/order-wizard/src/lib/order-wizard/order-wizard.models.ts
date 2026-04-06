import { AddressDto, PackageListItem, PaymentType, ServiceListItem } from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';

export interface RebookParams {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  selectedServiceNames: string[];
  selectedPackageNames: string[];
  rooms: number;
  bathrooms: number;
  address?: { street: string; city: string; zipCode: string; countryId: string; state: string };
}

export interface OrderWizardFormData {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  rooms: number;
  bathrooms: number;
  customerFirstName: string;
  customerLastName: string;
  customerEmail: string;
  customerPhone: string;
  address: AddressDto;
  cleaningDate: Date | null;
  cleaningTime: string;
  paymentType: PaymentType;
  extras: Record<string, boolean>;
  specialInstructions: string;
  entryInstructions: string;
}

export const ORDER_WIZARD_INITIAL_DATA: OrderWizardFormData = {
  selectedServiceIds: [],
  selectedPackageIds: [],
  rooms: 1,
  bathrooms: 1,
  customerFirstName: '',
  customerLastName: '',
  customerEmail: '',
  customerPhone: '',
  address: new AddressDto({ street: '', city: '', zipCode: '', countryId: '', state: '' }),
  cleaningDate: null,
  cleaningTime: '09:00',
  paymentType: PaymentType.Card,
  extras: {},
  specialInstructions: '',
  entryInstructions: '',
};

// ── Validation ──────────────────────────────────────────────

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_REGEX = /^[+]?[\d\s()-]{6,20}$/;
const ZIP_REGEX = /^[\d\s-]{3,20}$/;

export function getFieldError(
  field: string,
  data: OrderWizardFormData,
  translate: TranslateService
): string | null {
  switch (field) {
    case 'customerFirstName':
      if (!data.customerFirstName) return translate.instant('global.validation.required');
      if (data.customerFirstName.length < 2) return translate.instant('global.validation.minlength', { min: 2 });
      if (data.customerFirstName.length > 50) return translate.instant('global.validation.maxlength', { max: 50 });
      return null;
    case 'customerLastName':
      if (!data.customerLastName) return translate.instant('global.validation.required');
      if (data.customerLastName.length < 2) return translate.instant('global.validation.minlength', { min: 2 });
      if (data.customerLastName.length > 50) return translate.instant('global.validation.maxlength', { max: 50 });
      return null;
    case 'customerEmail':
      if (!data.customerEmail) return translate.instant('global.validation.required');
      if (!EMAIL_REGEX.test(data.customerEmail)) return translate.instant('global.validation.email');
      if (data.customerEmail.length > 50) return translate.instant('global.validation.maxlength', { max: 50 });
      return null;
    case 'customerPhone':
      if (!data.customerPhone) return translate.instant('global.validation.required');
      if (!PHONE_REGEX.test(data.customerPhone.replace(/\s/g, ''))) return translate.instant('global.validation.phone');
      return null;
    case 'street':
      if (!data.address.street) return translate.instant('global.validation.required');
      if (data.address.street.length < 5) return translate.instant('global.validation.minlength', { min: 5 });
      if (data.address.street.length > 255) return translate.instant('global.validation.maxlength', { max: 255 });
      return null;
    case 'city':
      if (!data.address.city) return translate.instant('global.validation.required');
      if (data.address.city.length < 2) return translate.instant('global.validation.minlength', { min: 2 });
      if (data.address.city.length > 100) return translate.instant('global.validation.maxlength', { max: 100 });
      return null;
    case 'zipCode':
      if (!data.address.zipCode) return translate.instant('global.validation.required');
      if (!ZIP_REGEX.test(data.address.zipCode)) return translate.instant('global.validation.zip');
      return null;
    default:
      return null;
  }
}

// ── Time helpers ────────────────────────────────────────────

export interface TimeOption {
  label: string;
  value: string;
}

export function generateTimeOptions(): TimeOption[] {
  const options: TimeOption[] = [];
  for (let h = 7; h <= 20; h++) {
    const hh = h.toString().padStart(2, '0');
    options.push({ label: `${hh}:00`, value: `${hh}:00` });
    if (h < 20) {
      options.push({ label: `${hh}:30`, value: `${hh}:30` });
    }
  }
  return options;
}

export function filterTimeOptionsForToday(
  allOptions: TimeOption[],
  selectedDate: Date | null
): TimeOption[] {
  if (!selectedDate) return allOptions;

  const now = new Date();
  const isToday =
    selectedDate.getFullYear() === now.getFullYear() &&
    selectedDate.getMonth() === now.getMonth() &&
    selectedDate.getDate() === now.getDate();

  if (!isToday) return allOptions;

  const currentMinutes = now.getHours() * 60 + now.getMinutes();
  return allOptions.filter((opt) => {
    const [h, m] = opt.value.split(':').map(Number);
    return h * 60 + m > currentMinutes;
  });
}

// ── Price formatting ────────────────────────────────────────

const CZK_FORMATTER = new Intl.NumberFormat('cs-CZ', {
  style: 'currency',
  currency: 'CZK',
  minimumFractionDigits: 0,
});

export function formatPrice(price: number): string {
  return CZK_FORMATTER.format(price);
}

// ── Translation helpers ─────────────────────────────────────

export function getItemTranslation(
  item: ServiceListItem | PackageListItem,
  field: string,
  translate: TranslateService
): string {
  const lang = translate.currentLang || translate.getDefaultLang();
  const translations = item.translations;
  if (translations && translations[lang]) {
    const translated = (translations[lang] as unknown as Record<string, string>)[field];
    if (translated) return translated;
  }
  return (item as unknown as Record<string, string>)[field] || '';
}
