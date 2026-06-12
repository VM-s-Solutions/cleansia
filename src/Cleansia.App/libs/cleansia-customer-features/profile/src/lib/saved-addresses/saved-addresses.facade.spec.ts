import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SavedAddressDto } from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { SavedAddressesFacade } from './saved-addresses.facade';

describe('SavedAddressesFacade', () => {
  let facade: SavedAddressesFacade;
  let store: {
    addresses: ReturnType<typeof signal<SavedAddressDto[]>>;
    loading: ReturnType<typeof signal<boolean>>;
    loaded: ReturnType<typeof signal<boolean>>;
    refresh: jest.Mock;
    setDefault: jest.Mock;
    delete: jest.Mock;
  };
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  beforeEach(() => {
    store = {
      addresses: signal<SavedAddressDto[]>([]),
      loading: signal(false),
      loaded: signal(false),
      refresh: jest.fn().mockResolvedValue(true),
      setDefault: jest.fn(),
      delete: jest.fn(),
    };
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        SavedAddressesFacade,
        { provide: SavedAddressStore, useValue: store },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });

    facade = TestBed.inject(SavedAddressesFacade);
  });

  it('refreshes the store on load so the screen always shows fresh data', () => {
    facade.load();
    expect(store.refresh).toHaveBeenCalledTimes(1);
  });

  it('re-exposes the store signals', () => {
    const address = SavedAddressDto.fromJS({ id: 'a1', label: 'Home' });
    store.addresses.set([address]);
    store.loading.set(true);

    expect(facade.addresses()).toEqual([address]);
    expect(facade.loading()).toBe(true);
  });

  it('shows a success snackbar after a successful set-default', async () => {
    store.setDefault.mockResolvedValue(true);

    await facade.setDefault('a1');

    expect(store.setDefault).toHaveBeenCalledWith('a1');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.saved_addresses.default_success'
    );
    expect(facade.mutating()).toBe(false);
  });

  it('stays silent when set-default fails (the store surfaces the error)', async () => {
    store.setDefault.mockResolvedValue(false);

    await facade.setDefault('a1');

    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(facade.mutating()).toBe(false);
  });

  it('shows a success snackbar after a successful delete', async () => {
    store.delete.mockResolvedValue(true);

    await facade.delete('a1');

    expect(store.delete).toHaveBeenCalledWith('a1');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.saved_addresses.delete_success'
    );
    expect(facade.mutating()).toBe(false);
  });

  it('stays silent when delete fails (the store surfaces the error)', async () => {
    store.delete.mockResolvedValue(false);

    await facade.delete('a1');

    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    expect(facade.mutating()).toBe(false);
  });

  it('ignores overlapping mutations while one is in flight', async () => {
    let resolveFirst!: (v: boolean) => void;
    store.setDefault.mockReturnValue(
      new Promise<boolean>((resolve) => (resolveFirst = resolve))
    );

    const first = facade.setDefault('a1');
    await facade.delete('a2');

    expect(store.delete).not.toHaveBeenCalled();

    resolveFirst(true);
    await first;
    expect(facade.mutating()).toBe(false);
  });
});
