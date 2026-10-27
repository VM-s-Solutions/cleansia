import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { OrderListItem } from '@cleansia/customer-services';
import { CUSTOMER_ORDER_FEATURE_KEY, customerOrderInitialState } from '@cleansia/customer-stores';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { OrderMarketFacade } from '../order-market.facade';
import { OrdersComponent } from './orders.component';

function row(id: string, isGuestBooking: boolean | undefined): OrderListItem {
  return OrderListItem.fromJS({
    id,
    displayOrderNumber: `CLS-${id}`,
    orderStatus: { type: 'OrderStatus', name: 'Confirmed', value: 2 },
    cleaningDateTime: '2026-11-02T09:00:00Z',
    customerAddress: 'Vinohradská 12, Praha',
    totalPrice: 1200,
    currency: { code: 'CZK' },
    isGuestBooking,
  });
}

/**
 * My orders lists the bookings made without an account under the session's e-mail beside the
 * account's own, and labels them; the server flags them, the list draws the flag.
 */
describe('OrdersComponent — guest bookings in My orders', () => {
  function render(orders: OrderListItem[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [OrdersComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        provideMockStore({
          initialState: {
            [CUSTOMER_ORDER_FEATURE_KEY]: { ...customerOrderInitialState, orders, totalRecords: orders.length },
          },
        }),
      ],
    }).overrideComponent(OrdersComponent, {
      set: {
        providers: [{ provide: OrderMarketFacade, useValue: { label: () => 'Czechia', destroyed$: new Subject<void>() } }],
      },
    });
    TestBed.inject(TranslateService).use('en');
    const fixture = TestBed.createComponent(OrdersComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const badgeOf = (page: HTMLElement, id: string): Element | null =>
    Array.from(page.querySelectorAll('.order-row'))
      .find((item) => item.textContent?.includes(`CLS-${id}`))
      ?.querySelector('.order-row__head .order-row__guest') ?? null;

  it('labels a guest booking in its row head', () => {
    const page = render([row('GUEST', true), row('OWN', false)]);

    expect(badgeOf(page, 'GUEST')?.textContent?.trim()).toBe('pages.orders.guest_booking');
  });

  it.each([false, undefined])('labels no booking of the account itself (flag %s)', (flag) => {
    const page = render([row('OWN', flag)]);

    expect(badgeOf(page, 'OWN')).toBeNull();
  });
});
