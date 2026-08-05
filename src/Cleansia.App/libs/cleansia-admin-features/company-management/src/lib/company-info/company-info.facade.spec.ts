import { TestBed } from '@angular/core/testing';
import { AdminClient, UpdateCompanyInfoCommand } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CompanyInfoFormData } from '../company-info.models';
import { CompanyInfoFacade } from './company-info.facade';

describe('CompanyInfoFacade', () => {
  let facade: CompanyInfoFacade;
  let getCurrentMock: jest.Mock;
  let updateMock: jest.Mock;
  let getOverviewMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const formData: CompanyInfoFormData = {
    legalName: 'Cleansia s.r.o.',
    tradingName: 'Cleansia',
    tagline: null,
    registrationNumber: '12345678',
    vatNumber: 'CZ12345678',
    street: 'Karlova 1',
    city: 'Praha',
    zipCode: '11000',
    countryId: 'country-1',
    phone: null,
    email: 'hello@cleansia.cz',
    website: null,
    bankName: null,
    bankAccountNumber: null,
    iban: 'CZ6520100000002100123456',
    swift: null,
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getCurrentMock = jest.fn().mockReturnValue(of({ id: 'company-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'company-1' }));
    getOverviewMock = jest.fn().mockReturnValue(of([]));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        CompanyInfoFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCompanyClient: {
              getCurrent: getCurrentMock,
              update: updateMock,
            },
            adminCountryClient: { getOverview: getOverviewMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(CompanyInfoFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.companyInfo()).toBeNull();
    expect(facade.countries()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.saving()).toBe(false);
  });

  it('settles loading and holds nothing when the read fails', () => {
    getCurrentMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadCompanyInfo();

    expect(facade.companyInfo()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('refuses to save before a company is loaded', () => {
    jest.spyOn(console, 'error').mockImplementation(() => undefined);

    facade.saveCompanyInfo(formData);

    expect(updateMock).not.toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('re-reads the company after a save lands, and not when it fails', () => {
    facade.loadCompanyInfo();
    expect(getCurrentMock).toHaveBeenCalledTimes(1);

    facade.saveCompanyInfo(formData);
    expect(getCurrentMock).toHaveBeenCalledTimes(2);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.company_info.messages.save_success'
    );

    updateMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.saveCompanyInfo(formData);
    expect(getCurrentMock).toHaveBeenCalledTimes(2);
    expect(facade.saving()).toBe(false);
  });

  describe('command bodies on the wire', () => {
    it('serializes the save against the loaded company id, blanks as undefined', () => {
      facade.loadCompanyInfo();
      facade.saveCompanyInfo(formData);

      const command: UpdateCompanyInfoCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateCompanyInfoCommand);
      expect(command.toJSON()).toEqual({
        companyInfoId: 'company-1',
        legalName: 'Cleansia s.r.o.',
        tradingName: 'Cleansia',
        tagline: undefined,
        registrationNumber: '12345678',
        vatNumber: 'CZ12345678',
        street: 'Karlova 1',
        city: 'Praha',
        zipCode: '11000',
        countryId: 'country-1',
        phone: undefined,
        email: 'hello@cleansia.cz',
        website: undefined,
        bankName: undefined,
        bankAccountNumber: undefined,
        iban: 'CZ6520100000002100123456',
        swift: undefined,
      });
    });
  });
});
