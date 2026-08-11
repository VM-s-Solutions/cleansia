import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  GetMyServingCleanersResponse,
} from '@cleansia/customer-services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { OrderPreferredCleanerFacade } from './order-preferred-cleaner.facade';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

const UNAVAILABLE_KEY = 'preferred_cleaner.unavailable';

function cleaner(
  employeeId: string,
  fullName: string,
  isAvailableForRequestedSlot?: boolean
): GetMyServingCleanersResponse {
  const row = new GetMyServingCleanersResponse();
  row.employeeId = employeeId;
  row.fullName = fullName;
  row.isAvailableForRequestedSlot = isAvailableForRequestedSlot;
  return row;
}

describe('OrderPreferredCleanerFacade', () => {
  let facade: OrderPreferredCleanerFacade;
  let orderClient: { myServingCleaners: jest.Mock };
  let formData: OrderWizardFormData;
  let isAuthenticated: boolean;
  let hasMembership: boolean;

  function build(platform: 'server' | 'browser' = 'browser'): void {
    orderClient = {
      myServingCleaners: jest
        .fn()
        .mockReturnValue(of([cleaner('emp-1', 'Anna Nováková', true)])),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        OrderPreferredCleanerFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { orderClient } },
        { provide: TranslateService, useValue: { instant: (key: string) => key } },
      ],
    });

    facade = TestBed.inject(OrderPreferredCleanerFacade);
    facade.connect({
      isAuthenticated: () => isAuthenticated,
      hasMembership: () => hasMembership,
      currentFormData: () => formData,
      patchFormData: (partial) => {
        formData = { ...formData, ...partial };
      },
    });
  }

  beforeEach(() => {
    isAuthenticated = true;
    hasMembership = true;
    formData = {
      ...ORDER_WIZARD_INITIAL_DATA,
      selectedServiceIds: ['svc-1'],
      selectedPackageIds: ['pkg-1'],
      cleaningDate: new Date('2026-09-01T00:00:00Z'),
      cleaningTime: '10:00',
    };
    build();
  });

  it('shows nothing before anything is loaded', () => {
    expect(facade.visible()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(facade.loadFailed()).toBe(false);
    expect(facade.options()).toEqual([]);
  });

  it('never asks for a roster on behalf of a guest', () => {
    isAuthenticated = false;

    facade.refresh();

    expect(orderClient.myServingCleaners).not.toHaveBeenCalled();
    expect(facade.visible()).toBe(false);
  });

  it('never asks for a roster for a customer without Plus', () => {
    hasMembership = false;

    facade.refresh();

    expect(orderClient.myServingCleaners).not.toHaveBeenCalled();
    expect(facade.visible()).toBe(false);
  });

  it('does not fetch during the server render', () => {
    build('server');

    facade.refresh();

    expect(orderClient.myServingCleaners).not.toHaveBeenCalled();
  });

  it('asks the slot question about the booking being composed', () => {
    facade.refresh();

    expect(orderClient.myServingCleaners).toHaveBeenCalledTimes(1);
    const [cleaningDateTimeUtc, serviceIds, packageIds] =
      orderClient.myServingCleaners.mock.calls[0];
    expect(serviceIds).toEqual(['svc-1']);
    expect(packageIds).toEqual(['pkg-1']);
    expect(cleaningDateTimeUtc).toBeInstanceOf(Date);
    expect((cleaningDateTimeUtc as Date).getHours()).toBe(10);
  });

  it('leaves the slot unasked until the customer has picked a date', () => {
    formData = { ...formData, cleaningDate: null };

    facade.refresh();

    expect(orderClient.myServingCleaners.mock.calls[0][0]).toBeUndefined();
  });

  it('renders the roster as options and becomes visible', () => {
    facade.refresh();

    expect(facade.loading()).toBe(false);
    expect(facade.visible()).toBe(true);
    expect(facade.options()).toEqual([
      { label: 'Anna Nováková', value: 'emp-1', disabled: false },
    ]);
  });

  it('stays hidden when the customer has no cleaners to ask for', () => {
    orderClient.myServingCleaners.mockReturnValue(of([]));

    facade.refresh();

    expect(facade.visible()).toBe(false);
    expect(facade.loadFailed()).toBe(false);
  });

  it('degrades to hidden rather than to an error banner on a failed roster read', () => {
    orderClient.myServingCleaners.mockReturnValue(throwError(() => new Error('boom')));

    facade.refresh();

    expect(facade.loadFailed()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.visible()).toBe(false);
    expect(facade.options()).toEqual([]);
  });

  it('marks a cleaner the slot cannot take, keeps the row and blocks the choice', () => {
    orderClient.myServingCleaners.mockReturnValue(
      of([cleaner('emp-1', 'Anna Nováková', false)])
    );

    facade.refresh();

    expect(facade.options()).toEqual([
      {
        label: `Anna Nováková · ${UNAVAILABLE_KEY}`,
        value: 'emp-1',
        disabled: true,
      },
    ]);
  });

  describe('selection', () => {
    beforeEach(() => facade.refresh());

    it('writes the choice onto the wizard form data', () => {
      facade.select('emp-1');

      expect(formData.preferredEmployeeId).toBe('emp-1');
      expect(facade.selectedEmployeeId()).toBe('emp-1');
    });

    it('clears the choice back to no preference', () => {
      facade.select('emp-1');
      facade.select(null);

      expect(formData.preferredEmployeeId).toBeNull();
    });

    it('drops a choice the newly chosen slot can no longer take, silently', () => {
      facade.select('emp-1');
      orderClient.myServingCleaners.mockReturnValue(
        of([cleaner('emp-1', 'Anna Nováková', false)])
      );

      facade.refresh();

      expect(formData.preferredEmployeeId).toBeNull();
    });

    it('drops a choice that has left the roster', () => {
      facade.select('emp-1');
      orderClient.myServingCleaners.mockReturnValue(
        of([cleaner('emp-2', 'Petr Svoboda', true)])
      );

      facade.refresh();

      expect(formData.preferredEmployeeId).toBeNull();
    });

    it('keeps a choice the fresh roster still admits', () => {
      facade.select('emp-1');

      facade.refresh();

      expect(formData.preferredEmployeeId).toBe('emp-1');
    });
  });
});
