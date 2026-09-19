import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminExtraDetailDto,
  CreateExtraCommand,
  UpdateExtraCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ExtraFormData, ExtraFormFacade } from './extra-form.facade';

describe('ExtraFormFacade', () => {
  let facade: ExtraFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let detailsMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;
  let getLanguagesMock: jest.Mock;
  let getCurrenciesMock: jest.Mock;

  const formData: ExtraFormData = {
    slug: 'inside-oven',
    name: 'Inside oven',
    description: 'Degrease',
    displayOrder: 10,
    prices: { CZK: 200, EUR: 8 },
    translations: {
      cs: { name: 'Trouba', description: '' },
      en: { name: '', description: '' },
    },
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ extraId: 'ext-1' }));
    updateMock = jest.fn().mockReturnValue(of({ extraId: 'ext-1' }));
    detailsMock = jest.fn().mockReturnValue(of(null));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();
    getLanguagesMock = jest.fn().mockReturnValue(of([]));
    getCurrenciesMock = jest.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        ExtraFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminExtraClient: {
              create: createMock,
              update: updateMock,
              details: detailsMock,
            },
            adminLanguageClient: { getOverview: getLanguagesMock },
            adminCurrencyClient: { getOverview: getCurrenciesMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(ExtraFormFacade);
  });

  it('reports success and returns to the list once a create lands', () => {
    facade.createExtra(formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.extra_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalledWith(['extra-management']);
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createExtra(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('reports success and returns to the list once an update lands', () => {
    facade.updateExtra('ext-1', formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.extra_form.messages.update_success'
    );
    expect(navigate).toHaveBeenCalledWith(['extra-management']);
    expect(facade.saving()).toBe(false);
  });

  it('stores the loaded extra', () => {
    const extra = AdminExtraDetailDto.fromJS({
      id: 'ext-1',
      slug: 'inside-oven',
      name: 'Inside oven',
      prices: { CZK: 200 },
    });
    detailsMock.mockReturnValue(of(extra));

    facade.loadExtra('ext-1');

    expect(detailsMock).toHaveBeenCalledWith('ext-1');
    expect(facade.extra()).toBe(extra);
    expect(facade.loading()).toBe(false);
  });

  it('returns to the list when the extra cannot be loaded', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadExtra('ext-1');

    expect(facade.extra()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['extra-management']);
  });

  // A 200 whose body is not a JSON array, and a 204, reach the subscriber as NULL from the
  // generated client even though the declared type is an array. Seeding a plausible array here
  // would assert nothing — that is exactly how this class of defect stayed hidden.
  describe('a null list from the generated client', () => {
    it('leaves the language list an empty array', () => {
      facade.languages.set([{ code: 'cs', name: 'Čeština' }]);
      getLanguagesMock.mockReturnValue(of(null));

      facade.loadLanguages();

      expect(facade.languages()).toEqual([]);
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

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead — the price map decides money per currency, and the
  // slug is the one identifier of an extra that crosses the wire, fixed at creation.
  describe('command bodies on the wire', () => {
    it('serializes a create with the slug, the prices per currency and only the filled translations', () => {
      facade.createExtra(formData);

      const command: CreateExtraCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateExtraCommand);
      expect(command.toJSON()).toEqual({
        slug: 'inside-oven',
        name: 'Inside oven',
        description: 'Degrease',
        displayOrder: 10,
        prices: { CZK: 200, EUR: 8 },
        translations: { cs: { name: 'Trouba' } },
      });
    });

    it('serializes an update with the extra id and without a slug', () => {
      facade.updateExtra('ext-1', formData);

      expect(updateMock.mock.calls[0][0]).toBe('ext-1');
      const command: UpdateExtraCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateExtraCommand);
      const json = command.toJSON();
      expect(json).not.toHaveProperty('slug');
      expect(json).toEqual({
        extraId: 'ext-1',
        name: 'Inside oven',
        description: 'Degrease',
        displayOrder: 10,
        prices: { CZK: 200, EUR: 8 },
        translations: { cs: { name: 'Trouba' } },
      });
    });
  });
});
