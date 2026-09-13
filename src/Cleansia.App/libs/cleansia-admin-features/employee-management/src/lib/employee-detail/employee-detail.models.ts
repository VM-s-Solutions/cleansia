import { EmployeeEntityType } from '@cleansia/admin-services';

export type EmployeeEditFormValue = Partial<{
  firstName: string | null;
  lastName: string | null;
  phoneNumber: string | null;
  birthDate: Date | null;
  street: string | null;
  city: string | null;
  zipCode: string | null;
  countryId: string | null;
  nationalityId: string | null;
  passportId: string | null;
  entityType: EmployeeEntityType | null;
  registrationNumber: string | null;
  legalEntityName: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
}>;

export interface EmployeePayConfigFormValue {
  serviceId: string | null;
  packageId: string | null;
  currencyId: string | null;
  basePay: number | null;
  extraPerRoom: number | null;
  extraPerBathroom: number | null;
  distanceRatePerKm: number | null;
  minimumPay: number | null;
  maximumPay: number | null;
  description: string | null;
}
