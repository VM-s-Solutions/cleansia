import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PackageFormData, PackageFormFacade } from './package-form.facade';

describe('PackageFormFacade', () => {
  let facade: PackageFormFacade;
  let updateMock: jest.Mock;
  let createMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: PackageFormData = {
    name: 'Move-out bundle',
    description: 'desc',
    price: 100,
    serviceIds: ['svc-a', 'svc-b'],
    translations: {},
  };

  beforeEach(() => {
    updateMock = jest.fn();
    createMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    const adminClient = {
      adminPackageClient: { update: updateMock, create: createMock },
    };

    TestBed.configureTestingModule({
      providers: [
        PackageFormFacade,
        { provide: AdminClient, useValue: adminClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(PackageFormFacade);
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
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
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

    expect(facade.errorKey()).toBe('errors.package.invalid_weight');
    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.package.invalid_weight'
    );
    expect(facade.saving()).toBe(false);
  });

  it('falls back to the generic update error for unknown codes', () => {
    updateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    facade.syncWeightRows([{ id: 'svc-a', name: 'A' }]);

    facade.updatePackage('pkg-1', formData);

    expect(facade.errorKey()).toBe('errors.package.update_failed');
    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.package.update_failed'
    );
  });
});
