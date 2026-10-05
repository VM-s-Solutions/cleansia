import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  ConfirmRecurringOrderCommand,
  ConfirmRecurringOrderResponse,
  ConsentType,
  CustomerAuthService,
  CustomerClient,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { OrderPreferredOfferComponent } from './components/order-preferred-offer.component';
import { OrderDetailComponent } from './order-detail.component';
import { OrderMarketFacade } from '../order-market.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-1';

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

function occurrence(paymentType: PaymentType, needsConfirmation = true): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: OrderStatus.New, name: OrderStatus[OrderStatus.New] },
    paymentType: { value: paymentType, name: PaymentType[paymentType] },
    paymentStatus: { value: PaymentStatus.Pending, name: PaymentStatus[PaymentStatus.Pending] },
    cleaningDateTime: '2026-10-09T08:00:00Z',
    totalPrice: 1200,
    currency: { code: 'CZK' },
    needsConfirmation,
  });
}

const consentInForce = (consentType: ConsentType) => ({
  consentType,
  isGranted: true,
  coversCurrentVersion: true,
});

describe('OrderDetailComponent — keeping the card when confirming a recurring occurrence', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;
  let orderClient: { getById: jest.Mock; confirmRecurring: jest.Mock };

  async function setup(booked: OrderItem, signedIn = true): Promise<void> {
    orderClient = {
      getById: jest.fn().mockReturnValue(of(booked)),
      confirmRecurring: jest.fn().mockReturnValue(
        of(ConfirmRecurringOrderResponse.fromJS({ orderId: ORDER_ID })),
      ),
    };
    await TestBed.configureTestingModule({
      imports: [
        OrderDetailComponent,
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: { getTranslation: () => of({}) } },
        }),
      ],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => ORDER_ID } } } },
        {
          provide: CustomerClient,
          useValue: {
            orderClient,
            gdprClient: {
              consentsGet: () =>
                of([consentInForce(ConsentType.TermsOfService), consentInForce(ConsentType.PrivacyPolicy)]),
            },
            membershipClient: { getMine: () => of(null) },
            receivableClient: { getMine: () => of([]) },
          },
        },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => signedIn } },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() },
        },
      ],
    })
      .overrideComponent(OrderDetailComponent, {
        remove: { imports: [OrderPreferredOfferComponent] },
        add: {
          imports: [PreferredOfferStub],
          providers: [
            { provide: OrderMarketFacade, useValue: { label: () => 'Czechia', destroyed$: new Subject<void>() } },
            { provide: OrderPreferredOfferFacade, useValue: { connect: jest.fn(), destroyed$: new Subject<void>() } },
          ],
        },
      })
      .compileComponents();

    TestBed.inject(TranslateService).use('en');
    fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const saveCardTick = (): HTMLElement | null =>
    (fixture.nativeElement as HTMLElement).querySelector('[data-spec-save-card]');
  const confirmButton = () => fixture.debugElement.query(By.css('.order-detail__actions cleansia-button'));
  const sentSaveCard = () =>
    (orderClient.confirmRecurring.mock.calls[0][0] as ConfirmRecurringOrderCommand).saveCard;

  it('offers an unticked box with the saved-card consent above the confirm button', async () => {
    await setup(occurrence(PaymentType.Card));

    const tick = saveCardTick();
    expect(tick?.textContent).toContain('pages.order.save_card.label');
    expect(tick?.textContent).toContain('pages.order.card_capture.consent.saved-card-draft-2026-10-05');
    expect(tick?.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(false);
    const position = tick?.compareDocumentPosition(confirmButton().nativeElement as Node) ?? 0;
    expect(position & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it.each([
    { what: 'a cash occurrence', booked: () => occurrence(PaymentType.Cash), signedIn: true },
    { what: 'a card order not awaiting confirmation', booked: () => occurrence(PaymentType.Card, false), signedIn: true },
    { what: 'a guest', booked: () => occurrence(PaymentType.Card), signedIn: false },
  ])('offers no tick to $what', async ({ booked, signedIn }) => {
    await setup(booked(), signedIn);

    expect(saveCardTick()).toBeNull();
  });

  it('confirms without keeping the card while the box is unticked', async () => {
    await setup(occurrence(PaymentType.Card));

    confirmButton().triggerEventHandler('onClick', new MouseEvent('click'));

    expect(sentSaveCard()).toBe(false);
  });

  it('confirms with the card kept once the customer ticks the box', async () => {
    await setup(occurrence(PaymentType.Card));

    fixture.debugElement
      .query(By.css('[data-spec-save-card] p-checkbox'))
      .triggerEventHandler('ngModelChange', true);
    confirmButton().triggerEventHandler('onClick', new MouseEvent('click'));

    expect(sentSaveCard()).toBe(true);
  });
});
