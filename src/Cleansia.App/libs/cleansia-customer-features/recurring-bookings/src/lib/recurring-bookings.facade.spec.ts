import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  DeleteRecurringBookingCommand,
  QuoteOrderResponse,
  RecurringBookingTemplateDto,
  SavedAddressDto,
  SetRecurringBookingActiveCommand,
} from '@cleansia/customer-services';
import {
  loadCustomerCurrencies,
  SavedAddressStore,
  selectCustomerDefaultCurrencyCode,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Observable, of, throwError } from 'rxjs';
import { RecurringBookingsFacade } from './recurring-bookings.facade';

describe('RecurringBookingsFacade', () => {
  let facade: RecurringBookingsFacade;
  let store: MockStore;
  let client: {
    getMine: jest.Mock;
    create: jest.Mock;
    setActive: jest.Mock;
    delete: jest.Mock;
  };
  let orderClient: { quote: jest.Mock };
  let savedAddressStore: {
    addresses: ReturnType<typeof signal<SavedAddressDto[]>>;
    loaded: ReturnType<typeof signal<boolean>>;
    refresh: jest.Mock;
  };
  let snackbar: {
    showError: jest.Mock;
    showSuccess: jest.Mock;
  };

  const template = (overrides?: Partial<RecurringBookingTemplateDto>): RecurringBookingTemplateDto =>
    RecurringBookingTemplateDto.fromJS({
      id: 't1',
      isActive: true,
      ...overrides,
    });

  beforeEach(() => {
    client = {
      getMine: jest.fn().mockReturnValue(of([])),
      create: jest.fn(),
      setActive: jest.fn().mockReturnValue(of(undefined)),
      delete: jest.fn().mockReturnValue(of(undefined)),
    };
    orderClient = { quote: jest.fn() };
    savedAddressStore = {
      addresses: signal<SavedAddressDto[]>([]),
      loaded: signal(true),
      refresh: jest.fn().mockResolvedValue(true),
    };
    snackbar = {
      showError: jest.fn(),
      showSuccess: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        RecurringBookingsFacade,
        provideMockStore(),
        {
          provide: CustomerClient,
          useValue: {
            recurringBookingClient: client,
            orderClient,
            membershipClient: { getMine: jest.fn().mockReturnValue(of({ hasMembership: true })) },
          },
        },
        { provide: SavedAddressStore, useValue: savedAddressStore },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    store = TestBed.inject(MockStore);
    store.overrideSelector(selectCustomerServices, []);
    store.overrideSelector(selectCustomerPackages, []);
    store.overrideSelector(selectCustomerDefaultCurrencyCode, null);
    facade = TestBed.inject(RecurringBookingsFacade);
  });

  describe('the currency a schedule is priced in', () => {
    it('asks the store for the platform currencies alongside the catalogue', async () => {
      jest.spyOn(store, 'dispatch');

      await facade.initialize();

      expect(store.dispatch).toHaveBeenCalledWith(loadCustomerCurrencies());
    });

    it('re-exposes the platform default for the catalogue prices the form lists', () => {
      store.overrideSelector(selectCustomerDefaultCurrencyCode, 'EUR');
      store.refreshState();

      expect(facade.defaultCurrencyCode()).toBe('EUR');
    });

    // The form's price threaded the quote's currency on one line and hardcoded CZK on the next.
    it("carries the quote's own currency onto the form price", async () => {
      orderClient.quote.mockReturnValue(
        of(QuoteOrderResponse.fromJS({ totalPrice: 1000, finalPriceAfterDiscount: 900, currencyCode: 'EUR' })),
      );
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      await facade.quoteForm();

      expect(facade.formPrice()).toEqual({ amount: 900, currency: 'EUR' });
    });

    it('prices a card in the currency its quote came back in', async () => {
      orderClient.quote.mockReturnValue(
        of(QuoteOrderResponse.fromJS({ totalPrice: 1000, finalPriceAfterDiscount: 1000, currencyCode: 'EUR' })),
      );

      await facade.quoteTemplate(template({ selectedServiceIds: ['s1'] }));

      expect(facade.templatePrices()['t1']).toEqual({ amount: 1000, currency: 'EUR' });
    });
  });

  describe('refreshList — the three data states', () => {
    it('starts empty and not loading', () => {
      expect(facade.templates()).toEqual([]);
      expect(facade.listLoading()).toBe(false);
      expect(facade.listLoaded()).toBe(false);
    });

    it('loads templates and clears loading on success', async () => {
      const t = template();
      client.getMine.mockReturnValue(of([t]));

      await facade.refreshList();

      expect(client.getMine).toHaveBeenCalledTimes(1);
      expect(facade.templates()).toEqual([t]);
      expect(facade.listLoaded()).toBe(true);
      expect(facade.listLoading()).toBe(false);
    });

    it('surfaces an error snackbar and clears loading on failure', async () => {
      client.getMine.mockReturnValue(throwError(() => new Error('boom')));

      await facade.refreshList();

      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.list_load_failed');
      expect(facade.listLoading()).toBe(false);
    });

    it('skips a concurrent refresh while one is in flight', async () => {
      let resolveFirst!: (v: RecurringBookingTemplateDto[]) => void;
      client.getMine.mockReturnValueOnce(
        new Observable<RecurringBookingTemplateDto[]>((sub) => {
          resolveFirst = (v) => {
            sub.next(v);
            sub.complete();
          };
        }),
      );

      const first = facade.refreshList();
      expect(facade.listLoading()).toBe(true);

      await facade.refreshList();
      expect(client.getMine).toHaveBeenCalledTimes(1);

      resolveFirst([]);
      await first;
      expect(facade.listLoading()).toBe(false);
    });
  });

  describe('toggleActive', () => {
    it('flips the template active flag optimistically on success', async () => {
      facade.templates.set([template({ id: 't1', isActive: true })]);

      await facade.toggleActive(template({ id: 't1', isActive: true }));

      expect(client.setActive).toHaveBeenCalledTimes(1);
      expect(facade.templates()[0].isActive).toBe(false);
      expect(facade.mutatingId()).toBeNull();
    });

    it('shows an error and clears the mutating flag on failure', async () => {
      client.setActive.mockReturnValue(throwError(() => new Error('boom')));
      facade.templates.set([template({ id: 't1', isActive: true })]);

      await facade.toggleActive(template({ id: 't1', isActive: true }));

      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.toggle_failed');
      expect(facade.mutatingId()).toBeNull();
    });

    it('ignores a toggle while another mutation is in flight', async () => {
      facade.mutatingId.set('other');

      await facade.toggleActive(template({ id: 't1' }));

      expect(client.setActive).not.toHaveBeenCalled();
    });
  });

  describe('deleteTemplate', () => {
    it('removes the template and shows success on a successful delete', async () => {
      facade.templates.set([template({ id: 't1' }), template({ id: 't2' })]);

      await facade.deleteTemplate('t1');

      expect(client.delete).toHaveBeenCalledTimes(1);
      expect(facade.templates().map((t) => t.id)).toEqual(['t2']);
      expect(snackbar.showSuccess).toHaveBeenCalledWith('recurring_booking.delete_success');
      expect(facade.mutatingId()).toBeNull();
    });

    it('shows an error and clears the mutating flag on a failed delete', async () => {
      client.delete.mockReturnValue(throwError(() => new Error('boom')));
      facade.templates.set([template({ id: 't1' })]);

      await facade.deleteTemplate('t1');

      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.delete_failed');
      expect(facade.mutatingId()).toBeNull();
    });
  });

  describe('submit', () => {
    beforeEach(() => {
      facade.updateFormData({
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-07-01T00:00:00Z'),
        selectedServiceIds: ['s1'],
      });
    });

    it('inserts the created template and shows success', async () => {
      const created = template({ id: 'new' });
      client.create.mockReturnValue(of(created));
      client.getMine.mockReturnValue(of([created]));

      const ok = await facade.submit();

      expect(ok).toBe(true);
      expect(client.create).toHaveBeenCalledTimes(1);
      expect(facade.templates().some((t) => t.id === 'new')).toBe(true);
      expect(snackbar.showSuccess).toHaveBeenCalledWith('recurring_booking.create_success');
      expect(facade.submitting()).toBe(false);
    });

    it('shows an error and returns false on a failed create', async () => {
      client.create.mockReturnValue(throwError(() => new Error('boom')));

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.create_failed');
      expect(facade.submitting()).toBe(false);
    });

    it('does not call the client when required fields are missing', async () => {
      facade.updateFormData({ savedAddressId: null });

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(client.create).not.toHaveBeenCalled();
    });
  });

  it('re-exposes the catalog signals from the NgRx store', () => {
    store.overrideSelector(selectCustomerServices, [
      { id: 's1' },
    ] as never);
    store.refreshState();

    expect(facade.services().length).toBe(1);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a toggle with the template id and the flipped flag', async () => {
      facade.templates.set([template({ id: 't1', isActive: true })]);

      await facade.toggleActive(template({ id: 't1', isActive: true }));

      const command: SetRecurringBookingActiveCommand =
        client.setActive.mock.calls[0][0];
      expect(command).toBeInstanceOf(SetRecurringBookingActiveCommand);
      expect(command.toJSON()).toEqual({ templateId: 't1', isActive: false });
    });

    it('serializes a delete with the template id', async () => {
      facade.templates.set([template({ id: 't1' })]);

      await facade.deleteTemplate('t1');

      const command: DeleteRecurringBookingCommand =
        client.delete.mock.calls[0][0];
      expect(command).toBeInstanceOf(DeleteRecurringBookingCommand);
      expect(command.toJSON()).toEqual({ templateId: 't1' });
    });
  });
});
