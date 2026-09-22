import {
  CreateCompanyInfoCommand,
  UpdateCompanyInfoCommand,
} from '@cleansia/admin-services';
import {
  buildCreateCompanyInfoCommand,
  buildUpdateCompanyInfoCommand,
  CompanyInfoFormData,
} from './company-info.models';

const filled: CompanyInfoFormData = {
  legalName: 'Cleansia s.r.o.',
  tradingName: 'Cleansia',
  tagline: 'Clean, every time',
  registrationNumber: '12345678',
  vatNumber: 'CZ12345678',
  street: 'Karlova 1',
  city: 'Praha',
  zipCode: '11000',
  countryId: 'country-1',
  phone: '+420123456789',
  email: 'hello@cleansia.cz',
  website: 'https://cleansia.cz',
  bankName: 'Fio banka',
  bankAccountNumber: '2100123456/2010',
  iban: 'CZ6520100000002100123456',
  swift: 'FIOBCZPP',
  isVatPayer: true,
  vatRegisteredFrom: new Date('2026-04-01T00:00:00.000Z'),
};

const blank: CompanyInfoFormData = {
  legalName: 'Cleansia s.r.o.',
  tradingName: 'Cleansia',
  tagline: null,
  registrationNumber: '12345678',
  vatNumber: null,
  street: 'Karlova 1',
  city: 'Praha',
  zipCode: '11000',
  countryId: 'country-1',
  phone: null,
  email: null,
  website: null,
  bankName: null,
  bankAccountNumber: null,
  iban: null,
  swift: null,
  isVatPayer: false,
  vatRegisteredFrom: null,
};

describe('company info command builders', () => {
  it('serializes a create with all eighteen fields', () => {
    const command = buildCreateCompanyInfoCommand(filled);

    expect(command).toBeInstanceOf(CreateCompanyInfoCommand);
    expect(command.toJSON()).toEqual({
      legalName: 'Cleansia s.r.o.',
      tradingName: 'Cleansia',
      tagline: 'Clean, every time',
      registrationNumber: '12345678',
      vatNumber: 'CZ12345678',
      street: 'Karlova 1',
      city: 'Praha',
      zipCode: '11000',
      countryId: 'country-1',
      phone: '+420123456789',
      email: 'hello@cleansia.cz',
      website: 'https://cleansia.cz',
      bankName: 'Fio banka',
      bankAccountNumber: '2100123456/2010',
      iban: 'CZ6520100000002100123456',
      swift: 'FIOBCZPP',
      isVatPayer: true,
      vatRegisteredFrom: '2026-04-01',
    });
  });

  it('serializes an update with the company id ahead of the same eighteen fields', () => {
    const command = buildUpdateCompanyInfoCommand('company-1', filled);

    expect(command).toBeInstanceOf(UpdateCompanyInfoCommand);
    expect(command.toJSON()).toEqual({
      companyInfoId: 'company-1',
      legalName: 'Cleansia s.r.o.',
      tradingName: 'Cleansia',
      tagline: 'Clean, every time',
      registrationNumber: '12345678',
      vatNumber: 'CZ12345678',
      street: 'Karlova 1',
      city: 'Praha',
      zipCode: '11000',
      countryId: 'country-1',
      phone: '+420123456789',
      email: 'hello@cleansia.cz',
      website: 'https://cleansia.cz',
      bankName: 'Fio banka',
      bankAccountNumber: '2100123456/2010',
      iban: 'CZ6520100000002100123456',
      swift: 'FIOBCZPP',
      isVatPayer: true,
      vatRegisteredFrom: '2026-04-01',
    });
  });

  it('sends every blank optional as undefined rather than null', () => {
    const create = buildCreateCompanyInfoCommand(blank);
    const update = buildUpdateCompanyInfoCommand('company-1', blank);

    for (const command of [create, update]) {
      const body = command.toJSON();
      expect(body.tagline).toBeUndefined();
      expect(body.vatNumber).toBeUndefined();
      expect(body.phone).toBeUndefined();
      expect(body.email).toBeUndefined();
      expect(body.website).toBeUndefined();
      expect(body.bankName).toBeUndefined();
      expect(body.bankAccountNumber).toBeUndefined();
      expect(body.iban).toBeUndefined();
      expect(body.swift).toBeUndefined();
    }
  });

  it('keeps the required fields present when every optional is blank', () => {
    expect(buildCreateCompanyInfoCommand(blank).toJSON()).toEqual({
      legalName: 'Cleansia s.r.o.',
      tradingName: 'Cleansia',
      tagline: undefined,
      registrationNumber: '12345678',
      vatNumber: undefined,
      street: 'Karlova 1',
      city: 'Praha',
      zipCode: '11000',
      countryId: 'country-1',
      phone: undefined,
      email: undefined,
      website: undefined,
      bankName: undefined,
      bankAccountNumber: undefined,
      iban: undefined,
      swift: undefined,
      isVatPayer: false,
      vatRegisteredFrom: undefined,
    });
  });

  // toEqual IGNORES keys whose value is undefined, so a field the builder forgot to set entirely
  // still satisfies every assertion above — which is exactly how two new required fields could be
  // added to the command and serialise as nothing at all without a single test noticing.
  // isVatPayer is a BOOLEAN the server reads to decide a tax posture: undefined there is not "false",
  // it is "the admin's choice never left the browser".
  it('sends isVatPayer as a real boolean, not as an omitted key', () => {
    for (const body of [
      buildCreateCompanyInfoCommand(blank).toJSON(),
      buildUpdateCompanyInfoCommand('company-1', blank).toJSON(),
      buildCreateCompanyInfoCommand(filled).toJSON(),
    ]) {
      expect(Object.prototype.hasOwnProperty.call(body, 'isVatPayer')).toBe(true);
      expect(typeof body.isVatPayer).toBe('boolean');
    }
  });

  it('carries the registration date only when the company is a payer', () => {
    expect(buildCreateCompanyInfoCommand(filled).toJSON().vatRegisteredFrom).toBe('2026-04-01');
    expect(buildCreateCompanyInfoCommand(blank).toJSON().vatRegisteredFrom).toBeUndefined();
  });
});
