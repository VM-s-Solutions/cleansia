import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CreateSavedCardCheckoutSessionCommand,
  CreateSavedCardCheckoutSessionResponse,
  CustomerClient,
  PaymentType,
} from '@cleansia/customer-services';
import { of, Subject, throwError } from 'rxjs';
import { OrderCardCaptureFacade } from './order-card-capture.facade';
import { OrderDraftService } from './order-draft.service';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

describe('OrderCardCaptureFacade', () => {
  let facade: OrderCardCaptureFacade;
  let savedCardClient: { createCheckoutSession: jest.Mock };
  let draft: { park: jest.Mock };
  let countryId: string | null;
  const REVIEW_STEP = 6;
  const booking: OrderWizardFormData = {
    ...ORDER_WIZARD_INITIAL_DATA,
    selectedServiceIds: ['s1'],
    paymentType: PaymentType.Cash,
  };

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
    draft = { park: jest.fn() };
    countryId = 'cz';

    TestBed.configureTestingModule({
      providers: [
        OrderCardCaptureFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { savedCardClient } },
        { provide: OrderDraftService, useValue: draft },
      ],
    });

    facade = TestBed.inject(OrderCardCaptureFacade);
    facade.connect({
      countryId: () => countryId,
      snapshot: () => ({ step: REVIEW_STEP, data: booking }),
    });
  }

  beforeEach(() => configure());

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

  it('sends the consent and the booking country, parks the booking and hands the browser to Stripe', () => {
    TestBed.resetTestingModule();
    configure('browser');
    facade.open();
    facade.setConsent(true);

    facade.start();

    expect(sentCommand().consentAccepted).toBe(true);
    expect(sentCommand().countryId).toBe('cz');
    expect(draft.park).toHaveBeenCalledWith(REVIEW_STEP, booking);
    expect(window.location.hash).toBe('#card-setup');
    expect(facade.starting()).toBe(true);
  });

  it('sends no country when the booking names none, so the server takes the default market', () => {
    countryId = null;
    facade.open();
    facade.setConsent(true);

    facade.start();

    expect(sentCommand().countryId).toBeUndefined();
  });

  it('parks nothing and stays open when the server refuses the capture', () => {
    savedCardClient.createCheckoutSession.mockReturnValue(
      throwError(() => ({ errors: { ConsentAccepted: 'saved_card.consent_not_accepted' } })),
    );
    facade.open();
    facade.setConsent(true);

    facade.start();

    expect(draft.park).not.toHaveBeenCalled();
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
