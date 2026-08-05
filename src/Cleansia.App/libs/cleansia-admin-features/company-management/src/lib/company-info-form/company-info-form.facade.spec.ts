import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCompanyInfoCommand,
  UpdateCompanyInfoCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CompanyInfoFormData } from '../company-info.models';
import { CompanyInfoFormFacade } from './company-info-form.facade';

describe('CompanyInfoFormFacade', () => {
  let facade: CompanyInfoFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let detailsMock: jest.Mock;
  let getOverviewMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: CompanyInfoFormData = {
    legalName: 'Cleansia s.r.o.',
    tradingName: 'Cleansia',
    tagline: 'Clean, every time',
    registrationNumber: '12345678',
    vatNumber: null,
    street: 'Karlova 1',
    city: 'Praha',
    zipCode: '11000',
    countryId: 'country-1',
    phone: '+420123456789',
    email: null,
    website: null,
    bankName: 'Fio banka',
    bankAccountNumber: '2100123456/2010',
    iban: null,
    swift: null,
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'company-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'company-1' }));
    detailsMock = jest.fn().mockReturnValue(of(null));
    getOverviewMock = jest.fn().mockReturnValue(of([]));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        CompanyInfoFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCompanyClient: {
              create: createMock,
              update: updateMock,
              details: detailsMock,
            },
            adminCountryClient: { getOverview: getOverviewMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(CompanyInfoFormFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.companyInfo()).toBeNull();
    expect(facade.countries()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.saving()).toBe(false);
  });

  it('settles loading and holds nothing when the detail read fails', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadCompanyInfo('company-1');

    expect(facade.companyInfo()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('keeps the country list empty when its read fails', () => {
    getOverviewMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadCountries();

    expect(facade.countries()).toEqual([]);
  });

  it('reports success and returns to the list once a create lands', () => {
    facade.createCompanyInfo(formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.company_info.messages.create_success'
    );
    expect(navigate).toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createCompanyInfo(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  it('clears saving and stays on the form when an update fails', () => {
    updateMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.updateCompanyInfo('company-1', formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  describe('command bodies on the wire', () => {
    it('serializes a create with every filled field and undefined for the blanks', () => {
      facade.createCompanyInfo(formData);

      const command: CreateCompanyInfoCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateCompanyInfoCommand);
      expect(command.toJSON()).toEqual({
        legalName: 'Cleansia s.r.o.',
        tradingName: 'Cleansia',
        tagline: 'Clean, every time',
        registrationNumber: '12345678',
        vatNumber: undefined,
        street: 'Karlova 1',
        city: 'Praha',
        zipCode: '11000',
        countryId: 'country-1',
        phone: '+420123456789',
        email: undefined,
        website: undefined,
        bankName: 'Fio banka',
        bankAccountNumber: '2100123456/2010',
        iban: undefined,
        swift: undefined,
      });
    });

    it('serializes an update with the company id ahead of the same body', () => {
      facade.updateCompanyInfo('company-1', formData);

      const command: UpdateCompanyInfoCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateCompanyInfoCommand);
      expect(command.toJSON()).toEqual({
        companyInfoId: 'company-1',
        legalName: 'Cleansia s.r.o.',
        tradingName: 'Cleansia',
        tagline: 'Clean, every time',
        registrationNumber: '12345678',
        vatNumber: undefined,
        street: 'Karlova 1',
        city: 'Praha',
        zipCode: '11000',
        countryId: 'country-1',
        phone: '+420123456789',
        email: undefined,
        website: undefined,
        bankName: 'Fio banka',
        bankAccountNumber: '2100123456/2010',
        iban: undefined,
        swift: undefined,
      });
    });
  });
});
