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

  const formData: ServiceFormData = {
    name: 'Deep clean',
    description: 'Full property deep clean',
    basePrice: 1200,
    perRoomPrice: 250,
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
            },
            adminLanguageClient: { getOverview: jest.fn().mockReturnValue(of([])) },
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

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031) — the price fields decide money.
  describe('command bodies on the wire', () => {
    it('serializes a create with the prices, the category and only the filled translations', () => {
      facade.createService(formData);

      const command: CreateServiceCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateServiceCommand);
      expect(command.toJSON()).toEqual({
        name: 'Deep clean',
        description: 'Full property deep clean',
        basePrice: 1200,
        perRoomPrice: 250,
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
        basePrice: 1200,
        perRoomPrice: 250,
        estimatedTime: 180,
        categoryId: 'cat-1',
        translations: {
          cs: { name: 'Hloubkové čištění', description: 'Celý byt' },
        },
      });
    });
  });
});
