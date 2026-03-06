import { AddressDto, PaymentType } from '@cleansia/partner-services';

export interface OrderWizardFormData {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  rooms: number;
  bathrooms: number;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  address: AddressDto;
  cleaningDate: Date | null;
  cleaningTime: string;
  paymentType: PaymentType;
  extras: Record<string, boolean>;
  specialInstructions: string;
}

export const ORDER_WIZARD_INITIAL_DATA: OrderWizardFormData = {
  selectedServiceIds: [],
  selectedPackageIds: [],
  rooms: 1,
  bathrooms: 1,
  customerName: '',
  customerEmail: '',
  customerPhone: '',
  address: new AddressDto({ street: '', city: '', zipCode: '', countryId: '', state: '' }),
  cleaningDate: null,
  cleaningTime: '09:00',
  paymentType: PaymentType.Card,
  extras: {},
  specialInstructions: '',
};
