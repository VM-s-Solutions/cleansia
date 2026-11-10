import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  CustomerAuditClient,
  PagedDataOfTimelineEntryDto,
  TimelineEntryDto,
  TimelineSource,
} from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { TimelineComponent } from './timeline.component';

describe('TimelineComponent', () => {
  let fixture: ComponentFixture<TimelineComponent>;
  let customerAuditClient: { timeline: jest.Mock };

  const page = PagedDataOfTimelineEntryDto.fromJS({
    data: [
      TimelineEntryDto.fromJS({
        source: TimelineSource.Customer,
        id: 'row-1',
        occurredOn: '2026-09-13T10:00:00Z',
        action: 'customer.order.cancel',
        resourceType: 'Order',
        resourceId: 'order-1',
        success: true,
      }),
    ],
    total: 60,
  });

  beforeEach(async () => {
    customerAuditClient = { timeline: jest.fn().mockReturnValue(of(page)) };

    await TestBed.configureTestingModule({
      imports: [TimelineComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: CustomerAuditClient, useValue: customerAuditClient },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TimelineComponent);
  });

  function offsetOfCall(index: number): number {
    return customerAuditClient.timeline.mock.calls[index][4];
  }

  it('loads the user timeline once when the user input arrives', () => {
    fixture.componentRef.setInput('userId', 'user-1');
    fixture.detectChanges();

    expect(customerAuditClient.timeline).toHaveBeenCalledTimes(1);
    expect(customerAuditClient.timeline.mock.calls[0][0]).toBe('user-1');
    expect(offsetOfCall(0)).toBe(0);
  });

  // The load effect reads the facade's paging signals through loadEntries; if they are tracked, every
  // page click re-runs the effect, which resets to page one and issues a second request.
  it('pages lazily with one request per page and keeps the requested offset', () => {
    fixture.componentRef.setInput('userId', 'user-1');
    fixture.detectChanges();

    fixture.componentInstance.onPageChange({ first: 40, rows: 20, page: 2, totalRecords: 60 });
    fixture.detectChanges();

    expect(customerAuditClient.timeline).toHaveBeenCalledTimes(2);
    expect(offsetOfCall(1)).toBe(40);
  });

  it('loads the resource timeline from the resource pair and restarts on a new resource', () => {
    fixture.componentRef.setInput('resourceType', 'Order');
    fixture.componentRef.setInput('resourceId', 'order-1');
    fixture.detectChanges();

    fixture.componentInstance.onPageChange({ first: 20, rows: 20, page: 1, totalRecords: 60 });
    fixture.detectChanges();

    fixture.componentRef.setInput('resourceId', 'order-2');
    fixture.detectChanges();

    expect(customerAuditClient.timeline).toHaveBeenCalledTimes(3);
    expect(customerAuditClient.timeline.mock.calls[2][2]).toBe('order-2');
    expect(offsetOfCall(2)).toBe(0);
  });

  describe('row click', () => {
    function renderRows(): HTMLTableRowElement[] {
      customerAuditClient.timeline.mockReturnValue(
        of(
          PagedDataOfTimelineEntryDto.fromJS({
            data: [
              { source: TimelineSource.Customer, id: 'row-1', resourceType: 'Order', resourceId: 'order-1' },
              { source: TimelineSource.Employee, id: 'row-2', resourceType: 'Order', resourceId: 'order-1' },
            ],
            total: 2,
          })
        )
      );
      fixture.componentRef.setInput('userId', 'user-1');
      fixture.detectChanges();
      return Array.from(fixture.nativeElement.querySelectorAll('tbody .table__row'));
    }

    it('opens a customer entry from its row, and leaves an employee row inert', () => {
      const viewEntry = jest.spyOn(fixture.componentInstance, 'viewEntry');
      const [customer, employee] = renderRows();

      expect(customer.getAttribute('tabindex')).toBe('0');
      expect(employee.getAttribute('tabindex')).toBeNull();
      (customer.querySelector('td') as HTMLElement).click();
      (employee.querySelector('td') as HTMLElement).click();

      expect(viewEntry).toHaveBeenCalledTimes(1);
      expect(viewEntry.mock.calls[0][0].id).toBe('row-1');
    });

    it('lets the resource link keep its own target instead of opening the entry', () => {
      const viewEntry = jest.spyOn(fixture.componentInstance, 'viewEntry');
      const [customer] = renderRows();

      (customer.querySelector('a.cleansia-audit-link') as HTMLAnchorElement).click();

      expect(viewEntry).not.toHaveBeenCalled();
    });
  });
});
