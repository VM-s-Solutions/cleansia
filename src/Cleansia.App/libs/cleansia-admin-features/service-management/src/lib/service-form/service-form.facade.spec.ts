import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateServiceCommand,
  UpdateServiceCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ServiceFormData, ServiceFormFacade } from './service-form.facade';

describe('ServiceFormFacade', () => {
  let facade: ServiceFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;
  let getLanguagesMock: jest.Mock;
  let getCategoriesMock: jest.Mock;
  let getCurrenciesMock: jest.Mock;

  const formData: ServiceFormData = {
    name: 'Deep clean',
    description: 'Full property deep clean',
    prices: {
      CZK: { basePrice: 1200, perRoomPrice: 250 },
      EUR: { basePrice: 48, perRoomPrice: 10 },
    },
    estimatedTime: 180,
    categoryId: 'cat-1',
    translations: {
      cs: { name: 'Hloubkové čištění', description: 'Celý byt' },
      en: { name: '', description: '' },
    },
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'svc-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'svc-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();
    getLanguagesMock = jest.fn().mockReturnValue(of([]));
    getCategoriesMock = jest.fn().mockReturnValue(of([]));
    getCurrenciesMock = jest.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        ServiceFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminServiceClient: {
              create: createMock,
              update: updateMock,
              details: jest.fn().mockReturnValue(of(null)),
              categories: getCategoriesMock,
            },
            adminLanguageClient: { getOverview: getLanguagesMock },
            adminCurrencyClient: { getOverview: getCurrenciesMock },
            adminCategoryClient: { getAll: jest.fn().mockReturnValue(of([])) },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(ServiceFormFacade);
  });

  it('reports success and returns to the list once a create lands', () => {
    facade.createService(formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.service_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createService(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  // A 200 whose body is not a JSON array, and a 204, reach the subscriber as NULL from the
  // generated client even though the declared type is an array. Seeding a plausible array here
  // would assert nothing — that is exactly how this class of defect stayed hidden.
  describe('a null list from the generated client', () => {
    it('leaves the language list an empty array', () => {
      // Seeded non-empty first, deliberately: an exception thrown inside a `subscribe` handler is
      // reported asynchronously and never reaches the signal, so a facade that still crashes on the
      // null would leave the signal at its INITIAL `[]` and an assertion against a fresh facade would
      // pass for the wrong reason. The seed makes the difference observable.
      facade.languages.set([{ code: 'cs', name: 'Čeština' }]);
      getLanguagesMock.mockReturnValue(of(null));

      facade.loadLanguages();

      expect(facade.languages()).toEqual([]);
    });

    it('leaves the category list an empty array', () => {
      facade.categories.set([{ id: 'cat-1', name: 'Deep clean' }]);
      getCategoriesMock.mockReturnValue(of(null));

      facade.loadCategories();

      expect(facade.categories()).toEqual([]);
    });

    it('leaves the currency list an empty array', () => {
      facade.currencies.set([
        { code: 'CZK', symbol: 'Kč', name: 'Czech koruna', isActive: true },
      ]);
      getCurrenciesMock.mockReturnValue(of(null));

      facade.loadCurrencies();

      expect(facade.currencies()).toEqual([]);
    });
  });

  /**
   * The form marks a block REQUIRED when its currency is operated and OPTIONAL when it is not, which
   * is exactly what the backend rule does — so the flag has to survive the mapping. A dropped
   * `isActive` reads as "nothing is required", and the admin learns the rule from a rejected save.
   */
  it('carries whether the platform operates in each currency', () => {
    getCurrenciesMock.mockReturnValue(
      of([
        { code: 'CZK', symbol: 'Kč', name: 'Czech koruna', isActive: true },
        { code: 'EUR', symbol: '€', name: 'Euro', isActive: false },
      ])
    );

    facade.loadCurrencies();

    expect(facade.currencies().map((c) => [c.code, c.isActive])).toEqual([
      ['CZK', true],
      ['EUR', false],
    ]);
  });

  // A currency's symbol and name are what the form's block headings read; the CODE is what the
  // backend keys the price row by, so a row with none is a price the upsert cannot place.
  it('falls back to the code when a currency arrives with no symbol or name', () => {
    getCurrenciesMock.mockReturnValue(
      of([{ code: 'CZK', isActive: true }, { symbol: '€', name: 'Euro' }])
    );

    facade.loadCurrencies();

    expect(facade.currencies()).toEqual([
      { code: 'CZK', symbol: 'CZK', name: 'CZK', isActive: true },
    ]);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031) — the price map decides money, and it now
  // decides it per currency: a map that loses a key silently takes the service off sale in that
  // market, which no type ever catches.
  describe('command bodies on the wire', () => {
    it('serializes a create with the prices, the category and only the filled translations', () => {
      facade.createService(formData);

      const command: CreateServiceCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateServiceCommand);
      expect(command.toJSON()).toEqual({
        name: 'Deep clean',
        description: 'Full property deep clean',
        prices: {
          CZK: { basePrice: 1200, perRoomPrice: 250 },
          EUR: { basePrice: 48, perRoomPrice: 10 },
        },
        estimatedTime: 180,
        categoryId: 'cat-1',
        translations: {
          cs: { name: 'Hloubkové čištění', description: 'Celý byt' },
        },
      });
    });

    it('serializes an update with the service id alongside every field', () => {
      facade.updateService('svc-1', formData);

      const command: UpdateServiceCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateServiceCommand);
      expect(command.toJSON()).toEqual({
        serviceId: 'svc-1',
        name: 'Deep clean',
        description: 'Full property deep clean',
        prices: {
          CZK: { basePrice: 1200, perRoomPrice: 250 },
          EUR: { basePrice: 48, perRoomPrice: 10 },
        },
        estimatedTime: 180,
        categoryId: 'cat-1',
        translations: {
          cs: { name: 'Hloubkové čištění', description: 'Celý byt' },
        },
      });
    });
  });
});
