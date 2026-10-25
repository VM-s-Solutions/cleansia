import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { CUSTOMER_API_BASE_URL, CustomerAuthService, LookupOrderResponse } from '@cleansia/customer-services';
import { OrderStatus } from '@cleansia/models';
import { TranslateService } from '@ngx-translate/core';
import { GuestOrderService } from './guest-order.service';
import { TrackOrderComponent } from './track-order.component';
import { TrackOrderFacade } from './track-order.facade';

const at = (minute: number): Date => new Date(Date.UTC(2026, 9, 25, 8, minute));

function lookedUp(current: OrderStatus, rows: [OrderStatus, number][]): LookupOrderResponse {
  return LookupOrderResponse.fromJS({
    id: 'ord-1',
    orderStatus: { value: current, name: OrderStatus[current] },
    statusHistory: rows.map(([status, minute]) => ({
      status: { value: status, name: OrderStatus[status] },
      createdOn: at(minute).toISOString(),
    })),
  });
}

/**
 * The guest's journey reads the booking's latest pass, as the signed-in order page does: after its
 * last cleaner left, a booking is New again and Confirmed is not ticked under it.
 */
describe('TrackOrderComponent — the order steps after a walk-back', () => {
  let facade: TrackOrderFacade;
  let component: TrackOrderComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TrackOrderFacade,
        provideHttpClient(),
        { provide: CUSTOMER_API_BASE_URL, useValue: 'https://api.test' },
        { provide: GuestOrderService, useValue: { getAll: () => [], save: jest.fn() } },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => false } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParams: {} } } },
        { provide: TranslateService, useValue: { currentLang: 'en', instant: (key: string) => key } },
      ],
    });
    facade = TestBed.inject(TrackOrderFacade);
    component = TestBed.runInInjectionContext(() => new TrackOrderComponent());
  });

  afterEach(() => facade.ngOnDestroy());

  const step = (key: string) => component.orderSteps().find((s) => s.key === key);

  it('leaves Confirmed unticked under the current New step once the booking went back to New', () => {
    facade.selectedOrder.set(lookedUp(OrderStatus.New, [[OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2]]));

    expect(step('new')).toMatchObject({ done: false, current: true, when: component.formatDate(at(0)) });
    expect(step('confirmed')).toMatchObject({ done: false, current: false, when: '' });
  });

  it('dates Confirmed by the new take once another cleaner took the booking', () => {
    facade.selectedOrder.set(
      lookedUp(OrderStatus.Confirmed, [
        [OrderStatus.New, 0],
        [OrderStatus.Confirmed, 1],
        [OrderStatus.New, 2],
        [OrderStatus.Confirmed, 3],
      ]),
    );

    expect(step('new')).toMatchObject({ done: true, current: false, when: component.formatDate(at(0)) });
    expect(step('confirmed')).toMatchObject({ done: false, current: true, when: component.formatDate(at(3)) });
  });

  it('leaves Confirmed unticked on a walked-back booking that was then cancelled', () => {
    facade.selectedOrder.set(
      lookedUp(OrderStatus.Cancelled, [
        [OrderStatus.New, 0],
        [OrderStatus.Confirmed, 1],
        [OrderStatus.New, 2],
        [OrderStatus.Cancelled, 3],
      ]),
    );

    expect(step('new')).toMatchObject({ done: true, current: false });
    expect(step('confirmed')).toMatchObject({ done: false, current: false, when: '' });
  });
});
