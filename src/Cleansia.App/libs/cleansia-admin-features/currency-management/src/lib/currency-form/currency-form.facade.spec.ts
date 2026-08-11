import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCurrencyCommand,
  UpdateCurrencyCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CurrencyFormData, CurrencyFormFacade } from './currency-form.facade';

describe('CurrencyFormFacade', () => {
  let facade: CurrencyFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: CurrencyFormData = {
    code: 'CZK',
    symbol: 'Kč',
    name: 'Czech koruna',
    exchangeRate: 1,
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'cur-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'cur-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        CurrencyFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCurrencyClient: {
              create: createMock,
              update: updateMock,
              details: jest.fn().mockReturnValue(of(null)),
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(CurrencyFormFacade);
  });

  it('reports success and returns to the list once a create lands', () => {
    facade.createCurrency(formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.currency_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createCurrency(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031) — a dropped exchange rate reprices
  // every order held in this currency.
  describe('command bodies on the wire', () => {
    it('serializes a create with the code, symbol, name and exchange rate', () => {
      facade.createCurrency(formData);

      const command: CreateCurrencyCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateCurrencyCommand);
      expect(command.toJSON()).toEqual({
        code: 'CZK',
        symbol: 'Kč',
        name: 'Czech koruna',
        exchangeRate: 1,
      });
    });

    it('serializes an update with the currency id alongside every field', () => {
      facade.updateCurrency('cur-1', { ...formData, exchangeRate: 24.5 });

      const command: UpdateCurrencyCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateCurrencyCommand);
      expect(command.toJSON()).toEqual({
        currencyId: 'cur-1',
        code: 'CZK',
        symbol: 'Kč',
        name: 'Czech koruna',
        exchangeRate: 24.5,
      });
    });
  });
});
