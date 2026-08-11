import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  SESSION_LIFECYCLE_LISTENERS,
} from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { SESSION_LIFECYCLE_PROVIDERS } from './session-listeners';

describe('customer session lifecycle wiring', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SESSION_LIFECYCLE_PROVIDERS,
        { provide: CustomerClient, useValue: { savedAddressClient: {} } },
        { provide: SnackbarService, useValue: { showErrorTranslated: jest.fn() } },
      ],
    });
  });

  it('resolves the saved-address store as a session listener', () => {
    const listeners = TestBed.inject(SESSION_LIFECYCLE_LISTENERS);

    expect(listeners).toContain(TestBed.inject(SavedAddressStore));
  });

  it('blanks the cached addresses when the session ends', () => {
    const store = TestBed.inject(SavedAddressStore);
    store.addresses.set([{ id: 'a' } as never]);
    store.loaded.set(true);

    TestBed.inject(SESSION_LIFECYCLE_LISTENERS).forEach((listener) =>
      listener.onSessionEnded()
    );

    expect(store.addresses()).toEqual([]);
    expect(store.loaded()).toBe(false);
  });
});
