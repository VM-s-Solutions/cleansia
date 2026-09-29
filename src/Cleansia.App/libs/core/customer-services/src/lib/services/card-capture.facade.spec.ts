import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import {
  CreateSavedCardCheckoutSessionCommand,
  CreateSavedCardCheckoutSessionResponse,
} from '../client/customer-client';
import { CardCaptureFacade, takeCardSetupReturnUrl } from './card-capture.facade';

describe('CardCaptureFacade', () => {
  let facade: CardCaptureFacade;
  let savedCardClient: { createCheckoutSession: jest.Mock };
  let park: jest.Mock;
  let countryId: string | null;
  const RETURN_URL = '/membership/recurring/create';

  function session(checkoutUrl: string): CreateSavedCardCheckoutSessionResponse {
    return CreateSavedCardCheckoutSessionResponse.fromJS({ savedCardId: 'card-1', checkoutUrl });
  }

  function sentCommand(): CreateSavedCardCheckoutSessionCommand {
    return savedCardClient.createCheckoutSession.mock.calls[0][0];
  }

  function configure(platform: 'server' | 'browser' = 'server'): void {
    const { origin, pathname } = window.location;
    savedCardClient = {
      createCheckoutSession: jest.fn().mockReturnValue(of(session(`${origin}${pathname}#card-setup`))),
    };
    park = jest.fn();
    countryId = 'cz';

    TestBed.configureTestingModule({
      providers: [
        CardCaptureFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { savedCardClient } },
      ],
    });

    facade = TestBed.inject(CardCaptureFacade);
    facade.connect({ countryId: () => countryId, park, returnUrl: () => RETURN_URL });
  }

  beforeEach(() => {
    sessionStorage.clear();
    configure();
  });

  it('opens with the consent unticked, even after an earlier tick', () => {
    facade.open();
    facade.setConsent(true);
    facade.close();

    facade.open();

    expect(facade.visible()).toBe(true);
    expect(facade.consentAccepted()).toBe(false);
  });

  it('asks Stripe for nothing until the consent is ticked', () => {
    facade.open();

    facade.start();

    expect(savedCardClient.createCheckoutSession).not.toHaveBeenCalled();
    expect(facade.starting()).toBe(false);
  });

  it('sends the consent and the booking country, parks the booking, remembers where it started and hands the browser to Stripe', () => {
    TestBed.resetTestingModule();
    configure('browser');
    facade.open();
    facade.setConsent(true);

    facade.start();

    expect(sentCommand().consentAccepted).toBe(true);
    expect(sentCommand().countryId).toBe('cz');
    expect(park).toHaveBeenCalledTimes(1);
    expect(window.location.hash).toBe('#card-setup');
    expect(facade.starting()).toBe(true);
    expect(takeCardSetupReturnUrl()).toBe(RETURN_URL);
    expect(takeCardSetupReturnUrl()).toBeNull();
  });

  it('sends no country when the booking names none, so the server takes the default market', () => {
    countryId = null;
    facade.open();
    facade.setConsent(true);

    facade.start();

    expect(sentCommand().countryId).toBeUndefined();
  });

  it('parks and remembers nothing, and stays open, when the server refuses the capture', () => {
    TestBed.resetTestingModule();
    configure('browser');
    savedCardClient.createCheckoutSession.mockReturnValue(
      throwError(() => ({ errors: { ConsentAccepted: 'saved_card.consent_not_accepted' } })),
    );
    facade.open();
    facade.setConsent(true);

    facade.start();

    expect(park).not.toHaveBeenCalled();
    expect(takeCardSetupReturnUrl()).toBeNull();
    expect(facade.starting()).toBe(false);
    expect(facade.visible()).toBe(true);
  });

  it('opens one Checkout Session while the first is still in flight', () => {
    savedCardClient.createCheckoutSession.mockReturnValue(new Subject<CreateSavedCardCheckoutSessionResponse>());
    facade.open();
    facade.setConsent(true);

    facade.start();
    facade.start();

    expect(savedCardClient.createCheckoutSession).toHaveBeenCalledTimes(1);
  });

  it('closes the step', () => {
    facade.open();

    facade.close();

    expect(facade.visible()).toBe(false);
  });
});
