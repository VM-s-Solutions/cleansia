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

function occurrence(paymentType: PaymentType): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: OrderStatus.New, name: OrderStatus[OrderStatus.New] },
    paymentType: { value: paymentType, name: PaymentType[paymentType] },
    paymentStatus: { value: PaymentStatus.Pending, name: PaymentStatus[PaymentStatus.Pending] },
    cleaningDateTime: '2026-10-09T08:00:00Z',
    totalPrice: 1200,
    currency: { code: 'CZK' },
    needsConfirmation: true,
  });
}

const consent = (consentType: ConsentType, coversCurrentVersion: boolean) => ({
  consentType,
  isGranted: true,
  coversCurrentVersion,
});

describe('OrderDetailComponent — the terms tick when confirming a recurring occurrence', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;
  let orderClient: { getById: jest.Mock; confirmRecurring: jest.Mock };

  async function setup(paymentType: PaymentType, termsCoverTextInForce: boolean): Promise<void> {
    orderClient = {
      getById: jest.fn().mockReturnValue(of(occurrence(paymentType))),
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
                of([
                  consent(ConsentType.TermsOfService, termsCoverTextInForce),
                  consent(ConsentType.PrivacyPolicy, true),
                ]),
            },
            membershipClient: { getMine: () => of(null) },
            receivableClient: { getMine: () => of([]) },
          },
        },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => true } },
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

  const termsTick = (): HTMLElement | null =>
    (fixture.nativeElement as HTMLElement).querySelector('[data-spec-recurring-terms]');
  const termsMissing = (): HTMLElement | null =>
    (fixture.nativeElement as HTMLElement).querySelector('[data-spec-terms-missing]');
  const confirmButton = () => fixture.debugElement.query(By.css('.order-detail__actions cleansia-button'));
  const sentTerms = () =>
    (orderClient.confirmRecurring.mock.calls[0][0] as ConfirmRecurringOrderCommand).termsAccepted;

  it.each([PaymentType.Cash, PaymentType.Card])(
    'asks an account behind on the terms for an unticked tick above the confirm, and holds the confirm (payment %s)',
    async (paymentType) => {
      await setup(paymentType, false);

      const tick = termsTick();
      expect(tick?.textContent).toContain('pages.order.accept_terms');
      expect(tick?.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(false);
      const position = tick?.compareDocumentPosition(confirmButton().nativeElement as Node) ?? 0;
      expect(position & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(confirmButton().componentInstance.disabled()).toBe(true);
      expect(termsMissing()?.textContent).toContain('pages.order.missing.terms');
    },
  );

  it('confirms with the tick asserted once the customer ticks it', async () => {
    await setup(PaymentType.Cash, false);

    fixture.debugElement
      .query(By.css('[data-spec-recurring-terms] p-checkbox'))
      .triggerEventHandler('ngModelChange', true);
    fixture.detectChanges();
    expect(confirmButton().componentInstance.disabled()).toBe(false);
    expect(termsMissing()).toBeNull();
    confirmButton().triggerEventHandler('onClick', new MouseEvent('click'));

    expect(sentTerms()).toBe(true);
  });

  it('asks nothing of an account whose consents cover the texts in force, and asserts nothing', async () => {
    await setup(PaymentType.Cash, true);

    expect(termsTick()).toBeNull();
    expect(termsMissing()).toBeNull();
    confirmButton().triggerEventHandler('onClick', new MouseEvent('click'));

    expect(sentTerms()).toBeUndefined();
  });
});
