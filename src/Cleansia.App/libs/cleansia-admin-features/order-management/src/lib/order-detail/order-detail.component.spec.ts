/* Child stubs mirror the real selectors and bindings so the override-imports swap is
   binding-compatible under the strict template test env. */
import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminClient,
  AdminGdprClient,
  CustomerAuditClient,
  OrderItem,
  UserItem,
} from '@cleansia/admin-services';
import { CleansiaButtonComponent } from '@cleansia/components';
import { PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import {
  AdminOrderOpsComponent,
  AdminOrderPhotosComponent,
  AdminOrderRefundComponent,
} from './components';
import { OrderDetailComponent } from './order-detail.component';
import { OrderDetailFacade } from './order-detail.facade';

@Component({ selector: 'cleansia-admin-order-ops', standalone: true, template: '' })
class OrderOpsStub {
  order = input.required<OrderItem>();
  changed = output<void>();
}

@Component({ selector: 'cleansia-admin-order-refund', standalone: true, template: '' })
class OrderRefundStub {
  order = input.required<OrderItem>();
  refunded = output<void>();
}

@Component({ selector: 'cleansia-admin-order-photos', standalone: true, template: '' })
class OrderPhotosStub {
  orderId = input.required<string>();
}

describe('OrderDetailComponent — incident file', () => {
  let details: jest.Mock;
  let customer: jest.Mock;
  let grantedPolicies: Set<string>;

  beforeEach(async () => {
    details = jest.fn().mockReturnValue(of(OrderItem.fromJS({ id: 'order-1' })));
    customer = jest.fn().mockReturnValue(of(null));
    grantedPolicies = new Set<string>(['CanAdminExportUserData', 'CanViewAuditLog', 'CanViewOrderCustomer']);

    await TestBed.configureTestingModule({
      imports: [OrderDetailComponent, TranslateModule.forRoot()],
      providers: [
        { provide: AdminClient, useValue: { adminOrderClient: { details, customer } } },
        { provide: AdminGdprClient, useValue: { incidentFile: jest.fn() } },
        { provide: CustomerAuditClient, useValue: { timeline: jest.fn() } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn(), showApiError: jest.fn() } },
        {
          provide: PermissionService,
          useValue: { hasPolicy: (p: string) => grantedPolicies.has(p) },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: (k: string) => (k === 'orderId' ? 'order-1' : null) } },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideComponent(OrderDetailComponent, {
        remove: {
          imports: [AdminOrderOpsComponent, AdminOrderPhotosComponent, AdminOrderRefundComponent],
        },
        add: { imports: [OrderOpsStub, OrderPhotosStub, OrderRefundStub] },
      })
      .compileComponents();
  });

  function render(): ComponentFixture<OrderDetailComponent> {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  function incidentButton(
    fixture: ComponentFixture<OrderDetailComponent>
  ): CleansiaButtonComponent | null {
    const button = fixture.debugElement.query(By.css('.cleansia-order-detail__incident-file'));
    return button ? (button.componentInstance as CleansiaButtonComponent) : null;
  }

  it('offers the incident file for the loaded order and hands the click to the facade', () => {
    const fixture = render();
    const facade = fixture.debugElement.injector.get(OrderDetailFacade);
    const exportIncidentFile = jest
      .spyOn(facade, 'exportIncidentFile')
      .mockImplementation(() => undefined);

    const button = incidentButton(fixture);
    expect(button).toBeTruthy();
    expect(button?.disabled()).toBe(false);

    fixture.componentInstance.exportIncidentFile();

    expect(exportIncidentFile).toHaveBeenCalledTimes(1);
  });

  it('keeps the incident file disabled until an order is loaded', () => {
    details.mockReturnValue(of(null));
    const fixture = render();

    expect(incidentButton(fixture)?.disabled()).toBe(true);
  });

  it('hides the incident file without CanAdminExportUserData', () => {
    grantedPolicies.delete('CanAdminExportUserData');
    const fixture = render();

    expect(incidentButton(fixture)).toBeNull();
    expect(fixture.debugElement.query(By.css('.cleansia-order-detail__audit-link'))).toBeTruthy();
  });

  // The lookup that precedes the file reads the audit timeline, which answers 403 without
  // CanViewAuditLog; a button that always fails is worse than none.
  it('hides the incident file without CanViewAuditLog', () => {
    grantedPolicies.delete('CanViewAuditLog');
    const fixture = render();

    expect(incidentButton(fixture)).toBeNull();
  });
  it('shows foreign customer fields read-only without an account navigation button', () => {
    details.mockReturnValue(of(OrderItem.fromJS({ id: 'order-1', customerCompany: 'Cleansia CZ', customerName: 'Order snapshot' })));
    customer.mockReturnValue(of(UserItem.fromJS({ id: 'foreign', email: 'must-not-render@example.test', lastName: 'MustNotRender', customerOfAnotherCompany: {
      id: 'foreign', firstName: 'Alice', maskedEmail: 'a***@mail.test', companyName: 'Cleansia CZ',
    } })));
    const fixture = render();
    const panel = fixture.debugElement.query(By.css('.cleansia-order-detail__customer-account'));
    expect(panel.nativeElement.textContent).toContain('Alice');
    expect(panel.nativeElement.textContent).toContain('a***@mail.test');
    expect(panel.nativeElement.textContent).not.toContain('must-not-render');
    expect(panel.nativeElement.textContent).not.toContain('MustNotRender');
    expect(fixture.debugElement.query(By.css('.cleansia-order-detail__customer-link'))).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Order snapshot');
  });

  it('offers the same-company customer button and navigates with its proven id', () => {
    customer.mockReturnValue(of(UserItem.fromJS({ id: 'same', firstName: 'Alice' })));
    const fixture = render();
    const button = fixture.debugElement.query(By.css('.cleansia-order-detail__customer-link'));
    expect(button).toBeTruthy();
    (button.componentInstance as CleansiaButtonComponent).onClick.emit(new MouseEvent('click'));
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['customers', 'same']);
  });

  it('renders loading, empty and failure states for the account independently of the order', () => {
    const pending = new Subject<UserItem>();
    customer.mockReturnValue(pending);
    const fixture = render();
    expect(fixture.debugElement.query(By.css('.cleansia-order-detail__customer-account cleansia-loader'))).toBeTruthy();
    pending.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('pages.order_detail.customer_account_empty');
    customer.mockReturnValue(throwError(() => new Error('offline')));
    fixture.debugElement.injector.get(OrderDetailFacade).loadOrderDetail('order-1');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('pages.order_detail.customer_account_error');
  });

  it('hides the panel and avoids its request without the customer policy', () => {
    grantedPolicies.delete('CanViewOrderCustomer');
    const fixture = render();
    expect(fixture.debugElement.query(By.css('.cleansia-order-detail__customer-account'))).toBeNull();
    expect(customer).not.toHaveBeenCalled();
  });

  // The only path to a seat with no acceptance is an admin's own placement; the line is where they
  // learn it, and Read opens the accepted text for the seat that has one.
  describe('the crew contract line', () => {
    const crewOrder = (): OrderItem =>
      OrderItem.fromJS({
        id: 'order-1',
        assignedEmployees: [
          { id: 'seat-1', employeeId: 'emp-1', fullName: 'Alice', phoneNumber: '+420 1' },
          { id: 'seat-2', employeeId: 'emp-2', fullName: 'Bob', phoneNumber: '+420 2' },
        ],
        workContractAcceptances: [
          {
            id: 'acc-1',
            orderEmployeeId: 'seat-1',
            employeeId: 'emp-1',
            acceptedOn: '2026-09-21T10:00:00Z',
            documentVersion: '2026-09-20',
            language: 'cs',
          },
        ],
      });

    it('shows accepted on the seat with a row and pending on the seat without one', () => {
      details.mockReturnValue(of(crewOrder()));
      const fixture = render();

      const cards = fixture.debugElement.queryAll(By.css('.employee-card'));
      expect(cards).toHaveLength(2);

      const accepted = cards[0].query(By.css('.employee-card__contract-state--accepted'));
      expect(accepted).toBeTruthy();
      expect(accepted.nativeElement.textContent).toContain('pages.order_detail.work_contract.accepted');
      expect(cards[0].query(By.css('.employee-card__contract-read'))).toBeTruthy();

      const pending = cards[1].query(By.css('.employee-card__contract-state--pending'));
      expect(pending).toBeTruthy();
      expect(pending.nativeElement.textContent).toContain('pages.order_detail.work_contract.pending');
      expect(cards[1].query(By.css('.employee-card__contract-read'))).toBeNull();
    });

    it("hands Read to the facade with the seat's acceptance id", () => {
      details.mockReturnValue(of(crewOrder()));
      const fixture = render();
      const facade = fixture.debugElement.injector.get(OrderDetailFacade);
      const read = jest.spyOn(facade, 'readWorkContract').mockImplementation(() => undefined);

      const button = fixture.debugElement.query(By.css('.employee-card__contract-read'));
      (button.componentInstance as CleansiaButtonComponent).onClick.emit(new MouseEvent('click'));

      expect(read).toHaveBeenCalledWith('acc-1');
    });

    it('renders no crew section for an order with no seats', () => {
      details.mockReturnValue(of(OrderItem.fromJS({ id: 'order-1', workContractAcceptances: [{ id: 'acc-x' }] })));
      const fixture = render();

      expect(fixture.debugElement.query(By.css('.employee-card'))).toBeNull();
    });
  });
});
