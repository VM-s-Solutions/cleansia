import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
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

const at = (minute: number): Date => new Date(Date.UTC(2026, 9, 25, 8, minute));

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

function order(
  current: OrderStatus,
  rows: [OrderStatus, number][],
  crew: { id: string; fullName: string }[] = [],
): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: current, name: OrderStatus[current] },
    paymentType: { value: PaymentType.Cash, name: 'Cash' },
    paymentStatus: { value: PaymentStatus.Pending, name: 'Pending' },
    cleaningDateTime: '2026-10-26T08:00:00Z',
    originalSubtotal: 2000,
    totalPrice: 2000,
    currency: { code: 'CZK' },
    selectedServices: [{ id: 's-1', name: 'Standard cleaning' }],
    selectedPackages: [],
    assignedEmployees: crew,
    statusHistory: rows.map(([status, minute]) => ({
      status: { value: status, name: OrderStatus[status] },
      createdOn: at(minute).toISOString(),
    })),
  });
}

/**
 * A booking whose last cleaner left goes back to New (ADR-0067), so its history holds a Confirmed row
 * from a take that no longer stands. The cleaning axis reads the booking's latest pass: Confirmed is
 * not ticked again until a cleaner takes it again, and is then dated by that take.
 */
describe('OrderDetailComponent — the cleaning axis after a walk-back', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;

  async function setup(booked: OrderItem): Promise<void> {
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
            orderClient: { getById: jest.fn().mockReturnValue(of(booked)) },
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

  afterEach(() => TestBed.resetTestingModule());

  const step = (key: string) => fixture.componentInstance.cleaningSteps().find((s) => s.key === key);

  it('leaves Confirmed unticked under a current Received once the booking went back to New', async () => {
    await setup(order(OrderStatus.New, [[OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2]]));

    expect(step('received')).toMatchObject({ done: true, current: true });
    expect(step('confirmed')).toMatchObject({ done: false, current: false, meta: undefined });
    expect(step('assigned')).toMatchObject({ done: false, current: false });
  });

  it('dates Confirmed by the new take once another cleaner took the booking', async () => {
    await setup(
      order(
        OrderStatus.Confirmed,
        [[OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2], [OrderStatus.Confirmed, 3]],
        [{ id: 'emp-2', fullName: 'Jana Nováková' }],
      ),
    );

    expect(step('confirmed')).toMatchObject({
      done: true,
      current: false,
      meta: fixture.componentInstance.formatDate(at(3)),
    });
    expect(step('assigned')).toMatchObject({ done: true, current: true, meta: 'Jana Nováková' });
  });

  it('leaves Confirmed unticked on a walked-back booking that was then cancelled, and dates the cancellation', async () => {
    await setup(
      order(OrderStatus.Cancelled, [[OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2], [OrderStatus.Cancelled, 3]]),
    );

    expect(step('confirmed')).toMatchObject({ done: false });
    expect(step('cancelled')).toMatchObject({
      done: true,
      current: true,
      meta: fixture.componentInstance.formatDate(at(3)),
    });
  });
});
