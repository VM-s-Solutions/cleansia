import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  GetMyMembershipResponse,
  OrderItem,
  OrderStatus,
  PreferredOfferState,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { OrderDetailComponent } from './order-detail.component';
import { OrderDetailFacade } from './order-detail.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-1';

function openOrder(): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    orderStatus: { value: OrderStatus.Confirmed, name: 'Confirmed' },
    cleaningDateTime: '2026-09-01T08:00:00Z',
    preferredOffer: {
      state: PreferredOfferState.Closed,
      cleanerName: 'Anna Nováková',
      canChooseAnother: true,
    },
  });
}

/**
 * The order and the membership are two reads that land in an order nothing controls. The button is
 * the server's single answer about the order, so it must settle when the order does and never
 * re-decide when a second, unrelated response arrives.
 */
describe('order detail wiring the preferred-offer facade', () => {
  let getById: Subject<OrderItem>;
  let getMine: Subject<GetMyMembershipResponse>;
  let component: OrderDetailComponent;
  let preferredOffer: OrderPreferredOfferFacade;

  beforeEach(() => {
    getById = new Subject<OrderItem>();
    getMine = new Subject<GetMyMembershipResponse>();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        OrderDetailComponent,
        OrderDetailFacade,
        OrderPreferredOfferFacade,
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: CustomerClient,
          useValue: {
            orderClient: { getById: () => getById.asObservable() },
            membershipClient: { getMine: () => getMine.asObservable() },
          },
        },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => true } },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() },
        },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => ORDER_ID } } },
        },
      ],
    });

    component = TestBed.inject(OrderDetailComponent);
    preferredOffer = TestBed.inject(OrderPreferredOfferFacade);
    component.ngOnInit();
  });

  it('settles the exit when the order lands and never re-decides when the membership follows', () => {
    const states: boolean[] = [];

    getById.next(openOrder());
    states.push(preferredOffer.canChooseAnother());

    getMine.next(
      GetMyMembershipResponse.fromJS({ hasMembership: true })
    );
    states.push(preferredOffer.canChooseAnother());

    expect(states).toEqual([true, true]);
  });

  it('holds that answer when the membership read fails outright', () => {
    getById.next(openOrder());
    getMine.error(new Error('boom'));

    expect(preferredOffer.canChooseAnother()).toBe(true);
  });

  // The recurring CTA is the surviving reader of this call — deleting it would take Path B with it.
  it('still reads the membership for the recurring CTA', () => {
    getById.next(openOrder());
    getMine.next(
      GetMyMembershipResponse.fromJS({ hasMembership: true })
    );

    expect(component.membership()?.hasMembership).toBe(true);
  });
});
