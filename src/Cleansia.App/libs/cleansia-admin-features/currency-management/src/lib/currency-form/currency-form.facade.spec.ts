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
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let navigate: jest.Mock;

  const formData: CurrencyFormData = {
    code: 'CZK',
    symbol: 'Kč',
    name: 'Czech koruna',
    loyaltyPointsDivisor: null,
    noShowCredit: null,
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'cur-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'cur-1' }));
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };
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

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
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
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031) — a currency saved without its code or
  // symbol is one nothing can be priced or displayed in.
  describe('command bodies on the wire', () => {
    it('serializes a create with the code, symbol and name', () => {
      facade.createCurrency(formData);

      const command: CreateCurrencyCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateCurrencyCommand);
      expect(command.toJSON()).toEqual({
        code: 'CZK',
        symbol: 'Kč',
        name: 'Czech koruna',
      });
    });

    it('serializes an update with the currency id alongside every field', () => {
      facade.updateCurrency('cur-1', { ...formData, name: 'Czech crown' });

      const command: UpdateCurrencyCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateCurrencyCommand);
      expect(command.toJSON()).toEqual({
        currencyId: 'cur-1',
        code: 'CZK',
        symbol: 'Kč',
        name: 'Czech crown',
      });
    });

    it('serializes the loyalty divisor on create and update, and omits it when empty', () => {
      facade.createCurrency({ ...formData, loyaltyPointsDivisor: 0.4 });
      facade.updateCurrency('cur-1', { ...formData, loyaltyPointsDivisor: null });

      const created: CreateCurrencyCommand = createMock.mock.calls[0][0];
      const updated: UpdateCurrencyCommand = updateMock.mock.calls[0][1];
      expect(created.toJSON().loyaltyPointsDivisor).toBe(0.4);
      expect(updated.toJSON().loyaltyPointsDivisor).toBeUndefined();
    });

    it('serializes the loyalty divisor on update when set', () => {
      facade.updateCurrency('cur-1', { ...formData, loyaltyPointsDivisor: 10 });

      const updated: UpdateCurrencyCommand = updateMock.mock.calls[0][1];
      expect(updated.toJSON().loyaltyPointsDivisor).toBe(10);
    });

    // The no-show credit is authored per currency like the divisor: absent on the wire reads as
    // null on the server, which is "no credit in this currency".
    it('serializes the no-show credit on create and update when typed', () => {
      facade.createCurrency({ ...formData, noShowCredit: 250 });
      facade.updateCurrency('cur-1', { ...formData, noShowCredit: 10 });

      const created: CreateCurrencyCommand = createMock.mock.calls[0][0];
      const updated: UpdateCurrencyCommand = updateMock.mock.calls[0][1];
      expect(created.toJSON().noShowCredit).toBe(250);
      expect(updated.toJSON().noShowCredit).toBe(10);
    });

    it('omits the no-show credit when left blank, so the server clears it', () => {
      facade.createCurrency(formData);
      facade.updateCurrency('cur-1', formData);

      const created: CreateCurrencyCommand = createMock.mock.calls[0][0];
      const updated: UpdateCurrencyCommand = updateMock.mock.calls[0][1];
      expect(created.toJSON().noShowCredit).toBeUndefined();
      expect(updated.toJSON().noShowCredit).toBeUndefined();
      expect(JSON.stringify(updated)).not.toContain('noShowCredit');
    });
  });
});
