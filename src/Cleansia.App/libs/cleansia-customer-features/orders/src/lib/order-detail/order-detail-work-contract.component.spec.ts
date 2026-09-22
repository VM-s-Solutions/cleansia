import { Component, input, PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { CustomerAuthService, CustomerClient, OrderItem, OrderStatus } from '@cleansia/customer-services';
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
    order_detail: {
      work_contract: {
        accepted_line: 'Contract for work accepted by {{name}} on {{date}}, version {{version}}',
        read: 'Read the contract',
      },
    },
  },
};

@Component({ selector: 'cleansia-customer-order-preferred-offer', standalone: true, template: '' })
class PreferredOfferStub {
  facade = input<unknown>();
  locale = input<string>();
}

function order(acceptances: Record<string, unknown>[]): OrderItem {
  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'ORD-1',
    orderStatus: { value: OrderStatus.Confirmed, name: 'Confirmed' },
    cleaningDateTime: '2026-09-25T08:00:00Z',
    totalPrice: 1200,
    currency: { code: 'CZK' },
    services: [],
    packages: [],
    assignedEmployees: [
      { id: 'seat-a', employeeId: 'emp-a', fullName: 'Petra' },
      { id: 'seat-b', employeeId: 'emp-b', fullName: 'Jana' },
    ],
    workContractAcceptances: acceptances,
  });
}

const ACCEPTANCE_A = {
  id: 'acc-a',
  orderEmployeeId: 'seat-a',
  employeeId: 'emp-a',
  acceptedOn: '2026-09-21T09:00:00Z',
  documentVersion: '2026-09-20',
  language: 'cs',
};

const ACCEPTANCE_B = {
  id: 'acc-b',
  orderEmployeeId: 'seat-b',
  employeeId: 'emp-b',
  acceptedOn: '2026-09-21T10:15:00Z',
  documentVersion: '2026-09-20',
  language: 'uk',
};

describe('OrderDetailComponent — the contract for work', () => {
  let fixture: ComponentFixture<OrderDetailComponent>;

  async function setup(acceptances: Record<string, unknown>[]): Promise<void> {
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
            orderClient: { getById: jest.fn().mockReturnValue(of(order(acceptances))) },
            membershipClient: { getMine: () => of(null) },
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

  const lines = (): HTMLElement[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.order-detail__contract'));

  it('states one line for the one cleaner who accepted, by given name, date and version', async () => {
    await setup([ACCEPTANCE_A]);

    expect(lines().length).toBe(1);
    const text = lines()[0].textContent?.replace(/\s+/g, ' ').trim();
    expect(text).toContain('Contract for work accepted by Petra on ');
    expect(text).toContain('version 2026-09-20');
    expect(text).toMatch(/2026/);
  });

  it('opens the accepted contract from its own acceptance id', async () => {
    await setup([ACCEPTANCE_A]);

    const link = lines()[0].querySelector<HTMLAnchorElement>('.order-detail__contract-link');
    expect(link?.textContent?.trim()).toBe('Read the contract');
    expect(link?.getAttribute('href')).toBe(`/orders/${ORDER_ID}/contract/acc-a`);
  });

  it('states one line per crew member who accepted', async () => {
    await setup([ACCEPTANCE_A, ACCEPTANCE_B]);

    expect(lines().map((line) => line.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      expect.stringContaining('accepted by Petra'),
      expect.stringContaining('accepted by Jana'),
    ]);
  });

  it('states nothing, and no placeholder, before any acceptance', async () => {
    await setup([]);

    expect(lines()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).querySelector('.order-detail__contracts')).toBeNull();
  });
});
