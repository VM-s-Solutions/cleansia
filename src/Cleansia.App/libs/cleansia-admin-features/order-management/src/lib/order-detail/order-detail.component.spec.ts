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
} from '@cleansia/admin-services';
import { CleansiaButtonComponent } from '@cleansia/components';
import { PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
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
  let grantedPolicies: Set<string>;

  beforeEach(async () => {
    details = jest.fn().mockReturnValue(of(OrderItem.fromJS({ id: 'order-1' })));
    grantedPolicies = new Set<string>(['CanAdminExportUserData', 'CanViewAuditLog']);

    await TestBed.configureTestingModule({
      imports: [OrderDetailComponent, TranslateModule.forRoot()],
      providers: [
        { provide: AdminClient, useValue: { adminOrderClient: { details } } },
        { provide: AdminGdprClient, useValue: { incidentFile: jest.fn() } },
        { provide: CustomerAuditClient, useValue: { timeline: jest.fn() } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn() } },
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
});
