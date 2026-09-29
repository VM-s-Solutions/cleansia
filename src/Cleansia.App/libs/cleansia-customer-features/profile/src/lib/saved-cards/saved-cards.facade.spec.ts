import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  CustomerClient,
  rememberCardSetupReturnUrl,
  RemoveSavedCardResponse,
  SavedCardDto,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { of, Subject, throwError } from 'rxjs';
import { SavedCardsFacade } from './saved-cards.facade';

describe('SavedCardsFacade', () => {
  let facade: SavedCardsFacade;
  let savedCardClient: { getMine: jest.Mock; remove: jest.Mock };
  let snackbar: { showSuccessTranslated: jest.Mock; showInfoTranslated: jest.Mock };
  let router: { navigateByUrl: jest.Mock };

  const visa = SavedCardDto.fromJS({
    id: 'card-1',
    brand: 'visa',
    last4: '4242',
    expMonth: 4,
    expYear: 2028,
    currencyCode: 'CZK',
  });

  beforeEach(() => {
    savedCardClient = {
      getMine: jest.fn().mockReturnValue(of([visa])),
      remove: jest.fn().mockReturnValue(of(RemoveSavedCardResponse.fromJS({ savedCardId: 'card-1' }))),
    };
    snackbar = { showSuccessTranslated: jest.fn(), showInfoTranslated: jest.fn() };
    router = { navigateByUrl: jest.fn().mockResolvedValue(true) };
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        SavedCardsFacade,
        { provide: CustomerClient, useValue: { savedCardClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: Router, useValue: router },
      ],
    });

    facade = TestBed.inject(SavedCardsFacade);
  });

  describe('the list', () => {
    it('shows each card by brand, last four digits, expiry and currency', () => {
      facade.init(null);

      expect(facade.cards()).toEqual([
        { id: 'card-1', brand: 'Visa', last4: '4242', expiry: '04/2028', currencyCode: 'CZK' },
      ]);
      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('is loading until the cards arrive', () => {
      const pending = new Subject<SavedCardDto[]>();
      savedCardClient.getMine.mockReturnValue(pending);

      facade.init(null);

      expect(facade.loading()).toBe(true);
      pending.next([]);
      pending.complete();
      expect(facade.loading()).toBe(false);
      expect(facade.cards()).toEqual([]);
    });

    it('says the read failed rather than claiming there is no card', () => {
      savedCardClient.getMine.mockReturnValue(throwError(() => new Error('offline')));

      facade.init(null);

      expect(facade.hasError()).toBe(true);
      expect(facade.cards()).toEqual([]);
      expect(facade.loading()).toBe(false);
    });

    it('clears the failure on a retry that succeeds', () => {
      savedCardClient.getMine.mockReturnValueOnce(throwError(() => new Error('offline')));
      facade.init(null);

      facade.load();

      expect(facade.hasError()).toBe(false);
      expect(facade.cards()).toHaveLength(1);
    });
  });

  describe('removing a card', () => {
    beforeEach(() => facade.init(null));

    it('removes it, says so and re-reads the list', () => {
      savedCardClient.getMine.mockReturnValue(of([]));

      facade.remove('card-1');

      expect(savedCardClient.remove).toHaveBeenCalledWith('card-1');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.profile.saved_cards.remove_success');
      expect(facade.cards()).toEqual([]);
      expect(facade.removingId()).toBeNull();
    });

    it('re-reads the list when the server refuses, leaving the refusal to the interceptor', () => {
      savedCardClient.remove.mockReturnValue(
        throwError(() => ({ errors: { SavedCardId: 'saved_card.not_found' } })),
      );
      savedCardClient.getMine.mockClear();

      facade.remove('card-1');

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(savedCardClient.getMine).toHaveBeenCalledTimes(1);
      expect(facade.removingId()).toBeNull();
    });

    it('sends one removal while the first is still in flight', () => {
      savedCardClient.remove.mockReturnValue(new Subject<RemoveSavedCardResponse>());

      facade.remove('card-1');
      facade.remove('card-1');

      expect(savedCardClient.remove).toHaveBeenCalledTimes(1);
      expect(facade.removingId()).toBe('card-1');
    });
  });

  describe('the return from the card-capture step', () => {
    it('says the card is saved and goes back to the page the capture started from', () => {
      rememberCardSetupReturnUrl('/order');

      facade.init('success');

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.profile.saved_cards.setup_success');
      expect(router.navigateByUrl).toHaveBeenCalledWith('/order', { replaceUrl: true });
      expect(savedCardClient.getMine).not.toHaveBeenCalled();
    });

    it('says no card was saved and goes back to the schedule the capture started from', () => {
      rememberCardSetupReturnUrl('/membership/recurring/create');

      facade.init('cancel');

      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith('pages.profile.saved_cards.setup_cancelled');
      expect(router.navigateByUrl).toHaveBeenCalledWith('/membership/recurring/create', { replaceUrl: true });
    });

    it('stays on the profile and lists the cards when no page was remembered', () => {
      facade.init('success');

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.profile.saved_cards.setup_success');
      expect(router.navigateByUrl).not.toHaveBeenCalled();
      expect(savedCardClient.getMine).toHaveBeenCalledTimes(1);
    });

    it('stays on the profile for any other value', () => {
      rememberCardSetupReturnUrl('/order');

      facade.init('elsewhere');

      expect(router.navigateByUrl).not.toHaveBeenCalled();
      expect(savedCardClient.getMine).toHaveBeenCalledTimes(1);
    });
  });
});
