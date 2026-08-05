import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { convertToParamMap, ParamMap } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { BehaviorSubject } from 'rxjs';
import { CheckoutSuccessComponent } from './checkout-success.component';

describe('CheckoutSuccessComponent', () => {
  let fixture: ComponentFixture<CheckoutSuccessComponent>;
  let queryParamMap: BehaviorSubject<ParamMap>;
  let title: Title;

  async function render(options: { type?: string; loggedIn?: boolean } = {}): Promise<void> {
    queryParamMap = new BehaviorSubject<ParamMap>(
      convertToParamMap(options.type === undefined ? {} : { type: options.type })
    );

    await TestBed.configureTestingModule({
      imports: [CheckoutSuccessComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap } },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => options.loggedIn ?? false } },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation(
      'en',
      {
        pages: {
          checkout: {
            success: {
              cash: { title: 'Booking confirmed' },
              card: { title: 'Payment received' },
            },
          },
        },
      },
      true
    );
    TestBed.inject(TranslateService).use('en');
    title = TestBed.inject(Title);

    fixture = TestBed.createComponent(CheckoutSuccessComponent);
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('payment type', () => {
    it('reads a cash return from the type query param', async () => {
      await render({ type: 'cash' });

      expect(fixture.componentInstance.isCash()).toBe(true);
    });

    it('treats an absent type param as a card return', async () => {
      await render();

      expect(fixture.componentInstance.isCash()).toBe(false);
    });

    it('treats an unrecognised type as a card return rather than cash', async () => {
      await render({ type: 'bank-transfer' });

      expect(fixture.componentInstance.isCash()).toBe(false);
    });

    it('tracks a later change of the query param', async () => {
      await render({ type: 'card' });

      queryParamMap.next(convertToParamMap({ type: 'cash' }));

      expect(fixture.componentInstance.isCash()).toBe(true);
    });
  });

  describe('document title', () => {
    it('announces the cash outcome on a cash return', async () => {
      await render({ type: 'cash' });

      expect(title.getTitle()).toBe('Booking confirmed | Cleansia');
    });

    it('announces the card outcome on a card return', async () => {
      await render({ type: 'card' });

      expect(title.getTitle()).toBe('Payment received | Cleansia');
    });
  });

  describe('orders link', () => {
    it('sends a signed-in customer to their own order list', async () => {
      await render({ loggedIn: true });

      expect(fixture.componentInstance.ordersRoute).toBe('/orders');
    });

    it('sends a guest to the public order tracker instead', async () => {
      await render({ loggedIn: false });

      expect(fixture.componentInstance.ordersRoute).toBe('/track-order');
    });
  });
});
