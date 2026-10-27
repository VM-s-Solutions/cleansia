/* Test doubles below mirror the real shared-component selectors so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
import { Component, CUSTOM_ELEMENTS_SCHEMA, forwardRef, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderItem, OrderStatus, PaymentStatus, PaymentType } from '@cleansia/partner-services';
import { TranslateModule, TranslatePipe, TranslateService } from '@ngx-translate/core';
import { OrderDetailsComponent } from './order-details.component';
import { OrderDetailsFacade } from './order-details.facade';

const EMPLOYEE_ID = 'emp-1';
const MARKED = '2026-10-27T10:40:00Z';

@Component({ selector: 'cleansia-section', standalone: true, template: '<ng-content />' })
class SectionStub {
  title = input<string>('');
}

@Component({ selector: 'cleansia-button', standalone: true, template: '{{ label() }}' })
class ButtonStub {
  label = input<string>('');
  icon = input<string | undefined>(undefined);
  severity = input<string>('primary');
  outlined = input<boolean>(false);
  text = input<boolean>(false);
  loading = input<boolean>(false);
  buttonSize = input<string>('medium');
  ariaLabel = input<string>('');
  iconPosition = input<string>('left');
  // The real button's output, which the sheet binds as (onClick).
  // eslint-disable-next-line @angular-eslint/no-output-on-prefix
  onClick = output<MouseEvent>();
}

@Component({
  selector: 'cleansia-text-input',
  standalone: true,
  template: '',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => TextInputStub), multi: true }],
})
class TextInputStub {
  label = input<string>('');
  floatVariant = input<string>('');
  dataType = input<string>('');
  disabled = input<boolean>(false);
  writeValue(): void {
    /* no-op */
  }
  registerOnChange(): void {
    /* no-op */
  }
  registerOnTouched(): void {
    /* no-op */
  }
  setDisabledState(): void {
    /* no-op */
  }
}

interface Seat {
  employeeId: string;
  fullName: string;
  completionConfirmedAt?: string;
}

function job(seats: Seat[], status = OrderStatus.InProgress): OrderItem {
  return OrderItem.fromJS({
    id: 'ord-1',
    displayOrderNumber: 'CLS-2026-1027',
    orderStatus: { type: 'order_status', name: OrderStatus[status], value: status },
    paymentStatus: { type: 'payment_status', name: 'Paid', value: PaymentStatus.Paid },
    paymentType: { type: 'payment_type', name: 'Card', value: PaymentType.Card },
    currency: { name: 'Czech koruna', code: 'CZK' },
    totalPrice: 2400,
    requiredEmployees: seats.length,
    assignedEmployees: seats.map((seat) => ({ id: `seat-${seat.employeeId}`, ...seat })),
    statusHistory: [],
  });
}

/**
 * Owner ruling 2026-10-26: on a job with two or more cleaners each marks their own part done on their
 * own device; once everyone on it has, a shared Complete appears to all of them and any one closes the
 * job. A one-cleaner job keeps its one Complete.
 */
describe('the job sheet of a job in progress', () => {
  let fixture: ComponentFixture<OrderDetailsComponent>;
  let facade: {
    orderDetails: ReturnType<typeof signal<OrderItem | null>>;
    loading: ReturnType<typeof signal<boolean>>;
    error: ReturnType<typeof signal<string | null>>;
    removal: ReturnType<typeof signal<null>>;
    currentEmployeeId: ReturnType<typeof signal<string | null>>;
    loadOrderDetails: jest.Mock;
    loadCurrentEmployee: jest.Mock;
    markMyPartDone: jest.Mock;
    completeOrder: jest.Mock;
    warnMissingAfterPhotos: jest.Mock;
  };

  function render(order: OrderItem, afterPhotos = true): HTMLElement {
    facade = {
      orderDetails: signal<OrderItem | null>(order),
      loading: signal(false),
      error: signal<string | null>(null),
      removal: signal(null),
      currentEmployeeId: signal<string | null>(EMPLOYEE_ID),
      loadOrderDetails: jest.fn(),
      loadCurrentEmployee: jest.fn(),
      markMyPartDone: jest.fn(),
      completeOrder: jest.fn(),
      warnMissingAfterPhotos: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [OrderDetailsComponent, TranslateModule.forRoot()],
      providers: [
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'ord-1' } } } },
      ],
    }).overrideComponent(OrderDetailsComponent, {
      set: {
        imports: [TranslatePipe, ReactiveFormsModule, SectionStub, ButtonStub, TextInputStub],
        schemas: [CUSTOM_ELEMENTS_SCHEMA],
        providers: [{ provide: OrderDetailsFacade, useValue: facade }],
      },
    });
    TestBed.inject(TranslateService).use('en');
    fixture = TestBed.createComponent(OrderDetailsComponent);
    fixture.detectChanges();
    if (afterPhotos) {
      (fixture.componentInstance as unknown as { onAfterPhotosPresenceChange(present: boolean): void })
        .onAfterPhotosPresenceChange(true);
      fixture.detectChanges();
    }
    return fixture.nativeElement as HTMLElement;
  }

  const buttons = (): string[] =>
    fixture.debugElement.queryAll(By.directive(ButtonStub)).map((button) => (button.nativeElement as HTMLElement).textContent?.trim() ?? '');
  const press = (labelKey: string): void => {
    const button = fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .find((candidate) => (candidate.nativeElement as HTMLElement).textContent?.trim() === labelKey);
    expect(button).toBeDefined();
    (button?.componentInstance as ButtonStub).onClick.emit(new MouseEvent('click'));
  };
  const crewLine = (page: HTMLElement): string =>
    page.querySelector('.cleansia-order-details__crew-close')?.textContent?.trim() ?? '';

  afterEach(() => TestBed.resetTestingModule());

  it('keeps the one Complete of a one-cleaner job, and no crew step', () => {
    const page = render(job([{ employeeId: EMPLOYEE_ID, fullName: 'Jan Novák' }]));

    expect(buttons()).toContain('pages.order_details.complete_order');
    expect(buttons()).not.toContain('pages.order_details.crew_close.mark_done');
    expect(page.querySelector('.cleansia-order-details__crew-close')).toBeNull();
  });

  describe('on a crew job', () => {
    const crew = (mine?: string, theirs?: string): Seat[] => [
      { employeeId: EMPLOYEE_ID, fullName: 'Jan Novák', completionConfirmedAt: mine },
      { employeeId: 'emp-2', fullName: 'Eva Svobodová', completionConfirmedAt: theirs },
    ];

    it('offers the caller I’m done and no Complete until they have marked their part', () => {
      const page = render(job(crew()));

      expect(buttons()).toContain('pages.order_details.crew_close.mark_done');
      expect(buttons()).not.toContain('pages.order_details.complete_order');
      expect(buttons()).not.toContain('pages.order_details.crew_close.complete_job');
      expect(crewLine(page)).toBe('pages.order_details.crew_close.mark_done_hint');

      press('pages.order_details.crew_close.mark_done');
      expect(facade.markMyPartDone).toHaveBeenCalledTimes(1);
    });

    it('still asks the caller to mark when only a crew-mate has', () => {
      render(job(crew(undefined, MARKED)));

      expect(buttons()).toContain('pages.order_details.crew_close.mark_done');
      expect(buttons()).not.toContain('pages.order_details.crew_close.complete_job');
    });

    it('has the caller who marked wait for the rest, with nothing to press', () => {
      const page = render(job(crew(MARKED)));

      expect(buttons()).not.toContain('pages.order_details.crew_close.mark_done');
      expect(buttons()).not.toContain('pages.order_details.crew_close.complete_job');
      expect(crewLine(page)).toBe('pages.order_details.crew_close.waiting');
    });

    it('offers every one of them the shared Complete once all have marked, which completes the job', () => {
      const page = render(job(crew(MARKED, MARKED)));

      expect(buttons()).toContain('pages.order_details.crew_close.complete_job');
      expect(buttons()).not.toContain('pages.order_details.crew_close.mark_done');
      expect(crewLine(page)).toBe('pages.order_details.crew_close.ready');

      press('pages.order_details.crew_close.complete_job');
      expect(facade.completeOrder).toHaveBeenCalledTimes(1);
    });

    it('withholds the shared Complete, as any Complete, until the job has an after photo', () => {
      render(job(crew(MARKED, MARKED)), false);

      expect(buttons()).not.toContain('pages.order_details.crew_close.complete_job');
    });

    it('shows when each cleaner marked their part, and who has not yet', () => {
      const page = render(job(crew(undefined, MARKED)));

      const rows = Array.from(page.querySelectorAll('.cleansia-order-details__employee-row')).map(
        (row) => row.textContent?.replace(/\s+/g, ' ').trim() ?? ''
      );
      expect(rows[0]).toContain('Jan Novák');
      expect(rows[0]).toContain('pages.order_details.employee_not_finished');
      expect(rows[1]).toContain('Eva Svobodová');
      expect(rows[1]).toContain('pages.order_details.employee_finished');
      expect(rows[1]).not.toContain('pages.order_details.employee_not_finished');
      expect(rows[1]).toMatch(/2026/);
    });

    it('says nothing of a mark on a crew job that is over without one', () => {
      const page = render(job(crew(), OrderStatus.Completed));

      expect(page.textContent).not.toContain('pages.order_details.employee_not_finished');
      expect(page.textContent).not.toContain('pages.order_details.employee_finished');
      expect(page.querySelector('.cleansia-order-details__crew-close')).toBeNull();
    });
  });
});
