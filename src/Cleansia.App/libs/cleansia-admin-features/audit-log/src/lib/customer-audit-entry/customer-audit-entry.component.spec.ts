import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import {
  CustomerActionAuditDetailDto,
  CustomerAuditClient,
} from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CustomerAuditEntryComponent } from './customer-audit-entry.component';

describe('CustomerAuditEntryComponent', () => {
  const hostileLabel = '<img src=x onerror=alert(1)>';

  function render(detail: CustomerActionAuditDetailDto): HTMLElement {
    TestBed.configureTestingModule({
      imports: [CustomerAuditEntryComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: CustomerAuditClient, useValue: { getById: jest.fn().mockReturnValue(of(detail)) } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'audit-1' } } },
        },
      ],
    });
    const fixture = TestBed.createComponent(CustomerAuditEntryComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders a client-controlled device label and payload value as literal text, never as markup', () => {
    const el = render(
      CustomerActionAuditDetailDto.fromJS({
        id: 'audit-1',
        userId: 'user-1',
        deviceLabel: hostileLabel,
        action: 'customer.order.cancel',
        success: true,
        occurredOn: '2026-09-13T10:00:00Z',
        payloadJson: JSON.stringify({ tier: hostileLabel }),
      })
    );

    expect(el.querySelector('img')).toBeNull();
    expect(el.textContent).toContain(hostileLabel);
    expect(el.querySelectorAll('.cleansia-audit-entry__diff tbody tr').length).toBe(1);
  });

  it('says beside the client name that the customer web and app share it, the device telling them apart', () => {
    const el = render(
      CustomerActionAuditDetailDto.fromJS({
        id: 'audit-1',
        clientAudience: 'cleansia.customer',
        action: 'customer.order.create',
        success: true,
        occurredOn: '2026-09-13T10:00:00Z',
      })
    );

    const hint = el.querySelector('.cleansia-audit-entry__meta-hint');
    expect(hint?.textContent).toContain('pages.audit_log.customers.entry.audience_hint');
    expect(hint?.parentElement?.textContent).toContain('cleansia.customer');
  });

  it('renders the payload as key/value rows with a raw JSON toggle, and no table without a payload', () => {
    const el = render(
      CustomerActionAuditDetailDto.fromJS({
        id: 'audit-1',
        action: 'customer.order.cancel',
        success: false,
        errorCode: 'order.in_progress_cannot_cancel',
        occurredOn: '2026-09-13T10:00:00Z',
      })
    );

    expect(el.querySelector('.cleansia-audit-entry__diff')).toBeNull();
    expect(el.querySelector('.cleansia-section__actions cleansia-button')).toBeNull();
    expect(el.textContent).toContain('order.in_progress_cannot_cancel');
  });
});
