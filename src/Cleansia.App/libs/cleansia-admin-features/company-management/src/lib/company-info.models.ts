import {
  CreateCompanyInfoCommand,
  UpdateCompanyInfoCommand,
} from '@cleansia/admin-services';

export interface CompanyInfoFormData {
  legalName: string;
  tradingName: string;
  tagline: string | null;
  registrationNumber: string;
  vatNumber: string | null;
  street: string;
  city: string;
  zipCode: string;
  countryId: string;
  phone: string | null;
  email: string | null;
  website: string | null;
  bankName: string | null;
  bankAccountNumber: string | null;
  iban: string | null;
  swift: string | null;
}

export function buildCreateCompanyInfoCommand(
  data: CompanyInfoFormData
): CreateCompanyInfoCommand {
  const command = new CreateCompanyInfoCommand();
  command.legalName = data.legalName;
  command.tradingName = data.tradingName;
  command.tagline = data.tagline ?? undefined;
  command.registrationNumber = data.registrationNumber;
  command.vatNumber = data.vatNumber ?? undefined;
  command.street = data.street;
  command.city = data.city;
  command.zipCode = data.zipCode;
  command.countryId = data.countryId;
  command.phone = data.phone ?? undefined;
  command.email = data.email ?? undefined;
  command.website = data.website ?? undefined;
  command.bankName = data.bankName ?? undefined;
  command.bankAccountNumber = data.bankAccountNumber ?? undefined;
  command.iban = data.iban ?? undefined;
  command.swift = data.swift ?? undefined;
  return command;
}

export function buildUpdateCompanyInfoCommand(
  companyInfoId: string,
  data: CompanyInfoFormData
): UpdateCompanyInfoCommand {
  const command = new UpdateCompanyInfoCommand();
  command.companyInfoId = companyInfoId;
  command.legalName = data.legalName;
  command.tradingName = data.tradingName;
  command.tagline = data.tagline ?? undefined;
  command.registrationNumber = data.registrationNumber;
  command.vatNumber = data.vatNumber ?? undefined;
  command.street = data.street;
  command.city = data.city;
  command.zipCode = data.zipCode;
  command.countryId = data.countryId;
  command.phone = data.phone ?? undefined;
  command.email = data.email ?? undefined;
  command.website = data.website ?? undefined;
  command.bankName = data.bankName ?? undefined;
  command.bankAccountNumber = data.bankAccountNumber ?? undefined;
  command.iban = data.iban ?? undefined;
  command.swift = data.swift ?? undefined;
  return command;
}
