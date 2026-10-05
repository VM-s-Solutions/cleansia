import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreatePackageCommand,
  UpdatePackageCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { PackageFormData, PackageFormFacade } from './package-form.facade';

// The keys a refusal resolves to; anything else falls back, as an untranslated code does in the app.
const TRANSLATED = new Set([
  'api.package.invalid_weight',
  'api.package.in_use',
  'api.package.update_failed',
]);

describe('PackageFormFacade', () => {
  let facade: PackageFormFacade;
  let updateMock: jest.Mock;
  let createMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let navigate: jest.Mock;
  let getLanguagesMock: jest.Mock;
  let getCurrenciesMock: jest.Mock;
  let getServicesMock: jest.Mock;

  const formData: PackageFormData = {
    name: 'Move-out bundle',
    description: 'desc',
    tagline: 'Handover day',
    isPopular: false,
    prices: { CZK: 100, EUR: 4 },
    serviceIds: ['svc-a', 'svc-b'],
    translations: {},
  };

  beforeEach(() => {
    updateMock = jest.fn();
    createMock = jest.fn();
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };
    navigate = jest.fn();

    getLanguagesMock = jest.fn().mockReturnValue(of([]));
    getCurrenciesMock = jest.fn().mockReturnValue(of([]));

    getServicesMock = jest.fn().mockReturnValue(of({ data: [], total: 0 }));

    const adminClient = {
      adminPackageClient: { update: updateMock, create: createMock },
      adminLanguageClient: { getOverview: getLanguagesMock },
      adminCurrencyClient: { getOverview: getCurrenciesMock },
      adminServiceClient: { getPaged: getServicesMock },
    };

    TestBed.configureTestingModule({
      providers: [
        PackageFormFacade,
        { provide: AdminClient, useValue: adminClient },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) =>
              k === 'enums.active_status.inactive'
                ? 'Inactive'
                : TRANSLATED.has(k)
                  ? `${k} (translated)`
                  : k,
            currentLang: 'en',
            onLangChange: EMPTY,
          },
        },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(PackageFormFacade);
  });

  it('offers every service with an id as a select option labelled by its name', () => {
    facade.availableServices.set([
      { id: 'svc-a', name: 'Windows' },
      { id: undefined, name: 'Unsaved' },
      { id: 'svc-b', name: undefined },
    ] as never);

    expect(facade.serviceOptions()).toEqual([
      { label: 'Windows', value: 'svc-a' },
      { label: '', value: 'svc-b' },
    ]);
  });

  /**
   * A retired service stays in the picker, marked, because a package may still include one; picking
   * one to ADD was refused only when the form was saved, as "Service not found".
   */
  describe('a retired service', () => {
    const WINDOWS = { id: 'svc-a', name: 'Windows' };
    const OVEN = { id: 'svc-r', name: 'Oven' };

    it('is offered marked Inactive and disabled, so it cannot be added', () => {
      facade.availableServices.set([WINDOWS, OVEN] as never);
      facade.retiredServiceIds.set(new Set(['svc-r']));

      expect(facade.serviceOptions()).toEqual([
        { label: 'Windows', value: 'svc-a' },
        { label: 'Oven (Inactive)', value: 'svc-r', disabled: true },
      ]);
      expect(facade.hasRetiredServices()).toBe(true);
    });

    it('stays enabled while the package includes it, so it can be taken out and put back', () => {
      facade.availableServices.set([WINDOWS, OVEN] as never);
      facade.retiredServiceIds.set(new Set(['svc-r']));
      facade.pkg.set({ includedServices: [{ ...OVEN, priceWeight: 1 }] } as never);

      expect(facade.serviceOptions()).toEqual([
        { label: 'Windows', value: 'svc-a' },
        { label: 'Oven (Inactive)', value: 'svc-r' },
      ]);
      expect(facade.serviceLabels().get('svc-r')).toBe('Oven (Inactive)');
    });

    it("is read from the services list's own inactive filter", () => {
      getServicesMock.mockImplementation((_search: unknown, isActive: unknown) =>
        of({ data: isActive === false ? [OVEN] : [WINDOWS, OVEN], total: 0 })
      );

      facade.loadAvailableServices();

      expect(getServicesMock).toHaveBeenCalledWith(undefined, false, undefined, 0, 1000);
      expect(facade.availableServices()).toEqual([WINDOWS, OVEN]);
      expect([...facade.retiredServiceIds()]).toEqual(['svc-r']);
    });

    it('is not marked when that read fails, and the picker still lists every service', () => {
      getServicesMock.mockImplementation((_search: unknown, isActive: unknown) =>
        isActive === false ? throwError(() => new Error('offline')) : of({ data: [WINDOWS, OVEN], total: 0 })
      );

      facade.loadAvailableServices();

      expect(facade.hasRetiredServices()).toBe(false);
      expect(facade.serviceOptions()).toEqual([
        { label: 'Windows', value: 'svc-a' },
        { label: 'Oven', value: 'svc-r' },
      ]);
    });
  });

  it('derives grosses that sum exactly to the package price for weights 3/1', () => {
    facade.setPrice(100);
    facade.syncWeightRows([
      { id: 'svc-a', name: 'A' },
      { id: 'svc-b', name: 'B' },
    ]);
    facade.setWeight('svc-a', 3);
    facade.setWeight('svc-b', 1);

    const grosses = facade.derivedGrosses();
    expect(grosses.map((g) => g.gross)).toEqual([75, 25]);
    expect(grosses.reduce((sum, g) => sum + g.gross, 0)).toBe(100);
  });

  it('splits evenly when every weight defaults to 1', () => {
    facade.setPrice(100);
    facade.syncWeightRows([
      { id: 'svc-a', name: 'A' },
      { id: 'svc-b', name: 'B' },
    ]);

    const grosses = facade.derivedGrosses();
    expect(grosses.map((g) => g.weight)).toEqual([1, 1]);
    expect(grosses.map((g) => g.gross)).toEqual([50, 50]);
    expect(grosses.reduce((sum, g) => sum + g.gross, 0)).toBe(100);
  });

  it('absorbs the sub-cent residual on the last row so grosses sum to the price', () => {
    facade.setPrice(100);
    facade.syncWeightRows([
      { id: 'svc-a', name: 'A' },
      { id: 'svc-b', name: 'B' },
      { id: 'svc-c', name: 'C' },
    ]);

    const grosses = facade.derivedGrosses();
    expect(grosses.reduce((sum, g) => sum + g.gross, 0)).toBe(100);
    expect(grosses[0].gross).toBe(33.33);
    expect(grosses[1].gross).toBe(33.33);
    expect(grosses[2].gross).toBe(33.34);
  });

  it('seeds weights from priceWeight on the detail dto', () => {
    facade.syncWeightRows(
      [
        { id: 'svc-a', name: 'A' },
        { id: 'svc-b', name: 'B' },
      ],
      [
        { id: 'svc-a', priceWeight: 4 },
        { id: 'svc-b', priceWeight: 2 },
      ]
    );

    expect(facade.weightRows().map((r) => r.weight)).toEqual([4, 2]);
  });

  it('sends serviceWeights in the update command', () => {
    updateMock.mockReturnValue(of({ id: 'pkg-1' }));
    facade.syncWeightRows([
      { id: 'svc-a', name: 'A' },
      { id: 'svc-b', name: 'B' },
    ]);
    facade.setWeight('svc-a', 3);
    facade.setWeight('svc-b', 1);

    facade.updatePackage('pkg-1', formData);

    expect(updateMock).toHaveBeenCalledTimes(1);
    const command = updateMock.mock.calls[0][1];
    expect(command.serviceWeights).toEqual({ 'svc-a': 3, 'svc-b': 1 });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.package_form.messages.update_success'
    );
  });

  it('defaults a non-positive weight to 1 in the update command', () => {
    updateMock.mockReturnValue(of({ id: 'pkg-1' }));
    facade.syncWeightRows([{ id: 'svc-a', name: 'A' }]);
    facade.setWeight('svc-a', 0);

    facade.updatePackage('pkg-1', formData);

    const command = updateMock.mock.calls[0][1];
    expect(command.serviceWeights).toEqual({ 'svc-a': 1 });
  });

  it('maps the invalid_weight backend code to its translation key', () => {
    updateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'package.invalid_weight' } }))
    );
    facade.syncWeightRows([{ id: 'svc-a', name: 'A' }]);

    facade.updatePackage('pkg-1', formData);

    expect(facade.errorKey()).toBe('api.package.invalid_weight');
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('falls back to result.title when detail is absent', () => {
    updateMock.mockReturnValue(
      throwError(() => ({ result: { title: 'package.invalid_weight' } }))
    );
    facade.syncWeightRows([{ id: 'svc-a', name: 'A' }]);

    facade.updatePackage('pkg-1', formData);

    expect(facade.errorKey()).toBe('api.package.invalid_weight');
  });

  it('parses the error code from a JSON response string', () => {
    updateMock.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'package.in_use' }),
      }))
    );
    facade.syncWeightRows([{ id: 'svc-a', name: 'A' }]);

    facade.updatePackage('pkg-1', formData);

    expect(facade.errorKey()).toBe('api.package.in_use');
  });

  it('falls back to the generic update error for unknown codes', () => {
    updateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    facade.syncWeightRows([{ id: 'svc-a', name: 'A' }]);

    facade.updatePackage('pkg-1', formData);

    expect(facade.errorKey()).toBe('api.package.update_failed');
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  // Seeded with `of(null)`, not a plausible array: the generated client answers a non-array 200
  // and a 204 with NULL. → service-form.facade.spec.ts
  it('leaves the language list an empty array when the client answers null', () => {
      // Seeded non-empty first so an unchanged signal cannot pass. → service-form.facade.spec.ts
    facade.languages.set([{ code: 'cs', name: 'Čeština' }]);
    getLanguagesMock.mockReturnValue(of(null));

    facade.loadLanguages();

    expect(facade.languages()).toEqual([]);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    const translatedData: PackageFormData = {
      ...formData,
      translations: {
        cs: { name: 'Balíček', description: 'Popis', tagline: 'Předání bytu' },
        en: { name: '', description: '', tagline: '' },
      },
    };

    it('serializes a create with a price per currency, the services and only the filled translations', () => {
      createMock.mockReturnValue(of({ id: 'pkg-1' }));

      facade.createPackage(translatedData);

      const command: CreatePackageCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreatePackageCommand);
      expect(command.toJSON()).toEqual({
        name: 'Move-out bundle',
        description: 'desc',
        tagline: 'Handover day',
        isPopular: false,
        prices: { CZK: 100, EUR: 4 },
        serviceIds: ['svc-a', 'svc-b'],
        translations: {
          cs: { name: 'Balíček', description: 'Popis', tagline: 'Předání bytu' },
        },
      });
    });

    it('serializes an update with the package id and the per-service weights', () => {
      updateMock.mockReturnValue(of({ id: 'pkg-1' }));
      facade.syncWeightRows([
        { id: 'svc-a', name: 'A' },
        { id: 'svc-b', name: 'B' },
      ]);
      facade.setWeight('svc-a', 3);

      facade.updatePackage('pkg-1', formData);

      const command: UpdatePackageCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdatePackageCommand);
      expect(command.toJSON()).toEqual({
        packageId: 'pkg-1',
        name: 'Move-out bundle',
        description: 'desc',
        tagline: 'Handover day',
        isPopular: false,
        prices: { CZK: 100, EUR: 4 },
        serviceIds: ['svc-a', 'svc-b'],
        serviceWeights: { 'svc-a': 3, 'svc-b': 1 },
        translations: {},
      });
    });
  });

  // The gross preview splits ONE number by weight, so it has to be denominated in something. The
  // default currency is the pick, and this is what names it -- a facade that returned the first
  // currency in the list would work in the seed and break the day a second one sorts ahead of it.
  describe('the currency the gross preview follows', () => {
    it('is the default one, not the first in the list', () => {
      getCurrenciesMock.mockReturnValue(
        of([
          { code: 'EUR', symbol: '€', name: 'Euro', isDefault: false },
          { code: 'CZK', symbol: 'Kč', name: 'Czech koruna', isDefault: true },
        ])
      );

      facade.loadCurrencies();

      expect(facade.defaultCurrencyCode()).toBe('CZK');
    });

    it('is null when no currency is marked default, rather than a wrong guess', () => {
      getCurrenciesMock.mockReturnValue(
        of([{ code: 'EUR', symbol: '€', name: 'Euro', isDefault: false }])
      );

      facade.loadCurrencies();

      expect(facade.defaultCurrencyCode()).toBeNull();
    });

    it('leaves the currency list an empty array when the client answers null', () => {
      facade.currencies.set([
        {
          code: 'CZK',
          symbol: 'Kč',
          name: 'Czech koruna',
          isDefault: true,
          isActive: true,
        },
      ]);
      getCurrenciesMock.mockReturnValue(of(null));

      facade.loadCurrencies();

      expect(facade.currencies()).toEqual([]);
    });

    // The block heading reads Required or Optional off this flag, matching what the backend rule
    // actually enforces. Drop it and every block reads optional.
    it('carries whether the platform operates in each currency', () => {
      getCurrenciesMock.mockReturnValue(
        of([
          { code: 'CZK', symbol: 'Kč', name: 'Czech koruna', isDefault: true, isActive: true },
          { code: 'EUR', symbol: '€', name: 'Euro', isDefault: false, isActive: false },
        ])
      );

      facade.loadCurrencies();

      expect(facade.currencies().map((c) => [c.code, c.isActive])).toEqual([
        ['CZK', true],
        ['EUR', false],
      ]);
    });
  });
});
