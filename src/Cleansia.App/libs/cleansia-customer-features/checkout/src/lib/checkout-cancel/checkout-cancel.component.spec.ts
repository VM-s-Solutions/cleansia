import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CheckoutCancelComponent } from './checkout-cancel.component';

/**
 * Without a way to pay now, the page's second link opens the customer's orders, which for a guest is
 * the public tracker, and it names the page it opens. No dictionary is loaded, so a key renders as
 * itself.
 */
describe('CheckoutCancelComponent — the orders link', () => {
  function render(loggedIn: boolean): HTMLAnchorElement | null {
    TestBed.configureTestingModule({
      imports: [CheckoutCancelComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({})) } },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => loggedIn } },
        { provide: CustomerClient, useValue: { paymentClient: { resumeCheckout: jest.fn() } } },
        { provide: SnackbarService, useValue: { showApiError: jest.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(CheckoutCancelComponent);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.cl-out__alt');
  }

  it('sends a signed-in customer to their own order list, saying so', () => {
    const link = render(true);

    expect(link?.getAttribute('href')).toBe('/orders');
    expect(link?.textContent?.trim()).toBe('pages.checkout.cancel.view_orders');
  });

  it('sends a guest to the public order tracker, named as the tracker', () => {
    const link = render(false);

    expect(link?.getAttribute('href')).toBe('/track-order');
    expect(link?.textContent?.trim()).toBe('nav.track_order');
  });
});
