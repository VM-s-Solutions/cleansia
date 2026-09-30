import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  DirtinessLevel,
  OrderItem,
  OrderStatus,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { OrderPreferredOfferComponent } from './components/order-preferred-offer.component';
import { OrderDetailComponent } from './order-detail.component';
import { OrderMarketFacade } from '../order-market.facade';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

const ORDER_ID = 'ord-1';

const COPY = {
  pages: {
    order: {
      dirtiness: {
        normal: { name: 'Normal' },
        increased: { name: 'Increased', surcharge_line: 'Increased dirtiness surcharge' },
        heavy: { name: 'Heavy', surcharge_line: 'Heavy dirtiness surcharge' },
      },
    },
    order_detail: {
      label_dirtiness: 'Dirtiness level',
    },
  },
};

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

function order(dirtinessLevel: DirtinessLevel, dirtinessSurchargeAmount: number): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: OrderStatus.Confirmed, name: 'Confirmed' },
    cleaningDateTime: '2026-09-25T08:00:00Z',
    originalSubtotal: 1000 + dirtinessSurchargeAmount,
    totalPrice: 1000 + dirtinessSurchargeAmount,
    currency: { code: 'CZK' },
    selectedServices: [{ id: 's-1', name: 'Standard cleaning' }],
    selectedPackages: [],
    dirtinessLevel,
    dirtinessSurchargeAmount,
  });
}

describe('OrderDetailComponent — the dirtiness level', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;

  async function setup(booked: OrderItem): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [
        OrderDetailComponent,
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: { getTranslation: () => of(COPY) } },
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

  const text = (el: Element | null): string => (el?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const levelFact = (): string =>
    text((fixture.nativeElement as HTMLElement).querySelector('[data-spec-dirtiness-level]'));
  const priceLines = (): string[][] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.order-detail__price-line')).map(
      (line) => Array.from(line.children).map(text),
    );

  it('names the level the order was booked at', async () => {
    await setup(order(DirtinessLevel.Increased, 300));

    expect(levelFact()).toBe('Dirtiness level Increased');
  });

  it('itemises the surcharge under its level and leaves the services line the rest of the subtotal', async () => {
    await setup(order(DirtinessLevel.Heavy, 600));

    expect(priceLines()).toEqual([
      ['Standard cleaning', 'CZK 1,000'],
      ['Heavy dirtiness surcharge', 'CZK 600'],
    ]);
  });

  it('names a normal level and adds no surcharge line when the level added nothing', async () => {
    await setup(order(DirtinessLevel.Normal, 0));

    expect(levelFact()).toBe('Dirtiness level Normal');
    expect(priceLines()).toEqual([['Standard cleaning', 'CZK 1,000']]);
  });
});
