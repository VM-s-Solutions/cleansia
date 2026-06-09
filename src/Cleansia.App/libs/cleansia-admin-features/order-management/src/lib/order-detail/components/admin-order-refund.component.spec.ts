/* Test doubles below intentionally mirror the real shared-component selectors
   and the PrimeNG-compatible `onClick` output so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-selector */
/* eslint-disable @angular-eslint/component-class-suffix */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output, signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { Subject } from 'rxjs';
import {
  OrderItem,
  OrderStatus,
  PaymentType,
  RefundReason,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { AdminOrderRefundComponent } from './admin-order-refund.component';
import { AdminOrderRefundFacade } from './admin-order-refund.facade';
import { RefundLineOption } from './admin-order-refund.models';

function valueAccessor(forwardTo: () => Type<unknown>) {
  return {
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(forwardTo),
    multi: true,
  };
}

class ControlStub {
  writeValue(): void {
    /* no-op */
  }
  registerOnChange(): void {
    /* no-op */
  }
  registerOnTouched(): void {
    /* no-op */
  }
}

@Component({
  selector: 'cleansia-section',
  standalone: true,
  template: '<ng-content />',
})
class SectionStub {
  title = input<string>('');
}

@Component({
  selector: 'cleansia-checkbox',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => CheckboxStub)],
})
class CheckboxStub extends ControlStub {
  label = input<string>('');
  valueChanges = output<boolean>();
}

@Component({
  selector: 'cleansia-select',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => SelectStub)],
})
class SelectStub extends ControlStub {
  label = input<string>('');
  options = input<ICleansiaSelectOption[]>([]);
  showClear = input<boolean>(true);
  valueChanges = output<unknown>();
}

@Component({
  selector: 'cleansia-textarea',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextareaStub)],
})
class TextareaStub extends ControlStub {
  label = input<string>('');
  placeholder = input<string>('');
  valueChanges = output<string>();
}

@Component({ selector: 'cleansia-button', standalone: true, template: '' })
class ButtonStub {
  label = input<string>('');
  icon = input<string>('');
  severity = input<string>('');
  loading = input<boolean>(false);
  disabled = input<boolean>(false);
  onClick = output<void>();
}

class FacadeStub {
  readonly destroyed$ = new Subject<void>();
  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }
  readonly lines = signal<RefundLineOption[]>([]);
  readonly reason = signal<RefundReason | null>(null);
  readonly overrideReason = signal<string>('');
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);
  readonly hasSelection = signal<boolean>(false);
  readonly canSubmit = signal<boolean>(false);
  setLines = jest.fn((lines: RefundLineOption[]) => this.lines.set(lines));
  toggleLine = jest.fn();
  setReason = jest.fn();
  setOverrideReason = jest.fn();
  submit = jest.fn();
}

function makeOrder(partial: Partial<OrderItem>): OrderItem {
  return OrderItem.fromJS({
    id: 'order-1',
    orderStatus: { value: OrderStatus.Completed },
    paymentType: { value: PaymentType.Card },
    selectedServices: [{ id: 'service-1', name: 'Deep clean' }],
    selectedPackages: [
      {
        id: 'package-1',
        name: 'Move-out bundle',
        includedServiceItems: [
          { id: 'service-2', name: 'Window cleaning' },
          { id: 'service-3', name: 'Oven cleaning' },
        ],
      },
    ],
    ...partial,
  });
}

describe('AdminOrderRefundComponent', () => {
  let fixture: ComponentFixture<AdminOrderRefundComponent>;
  let component: AdminOrderRefundComponent;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [AdminOrderRefundComponent, TranslateModule.forRoot()],
    })
      .overrideComponent(AdminOrderRefundComponent, {
        remove: {
          imports: [
            CleansiaSectionComponent,
            CleansiaCheckboxComponent,
            CleansiaSelectComponent,
            CleansiaTextareaComponent,
            CleansiaButtonComponent,
          ],
        },
        add: {
          imports: [SectionStub, CheckboxStub, SelectStub, TextareaStub, ButtonStub],
          providers: [{ provide: AdminOrderRefundFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(AdminOrderRefundComponent);
    component = fixture.componentInstance;
  });

  function setOrder(order: OrderItem): void {
    fixture.componentRef.setInput('order', order);
    fixture.detectChanges();
  }

  it('uses OnPush change detection', () => {
    const meta = (
      AdminOrderRefundComponent as unknown as { ɵcmp: { onPush: boolean } }
    ).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('renders the unavailable state for a non-card or non-completed order', () => {
    setOrder(makeOrder({ paymentType: { value: PaymentType.Cash } as never }));

    expect(component.isRefundable()).toBe(false);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('pages.order_management.refund.unavailable');
  });

  it('renders the empty state when a refundable order has no lines', () => {
    setOrder(makeOrder({ selectedServices: [], selectedPackages: [] }));

    expect(component.isRefundable()).toBe(true);
    expect(facade.lines().length).toBe(0);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('pages.order_management.refund.no_lines');
  });

  it('renders the refund form with line options for a refundable order', () => {
    setOrder(makeOrder({}));

    expect(component.isRefundable()).toBe(true);
    expect(facade.lines().length).toBe(3);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('pages.order_management.refund.select_lines');

    const checkboxes = fixture.debugElement.queryAll(By.directive(CheckboxStub));
    expect(checkboxes).toHaveLength(3);
    const button = fixture.debugElement.query(By.directive(ButtonStub))
      .componentInstance as ButtonStub;
    expect(button.label()).toBe('pages.order_management.refund.submit');
  });

  it("renders a package's includedServiceItems as selectable bundled rows", () => {
    setOrder(makeOrder({}));

    const groups = component.packageGroups();
    expect(groups).toHaveLength(1);
    expect(groups[0].packageId).toBe('package-1');
    expect(groups[0].packageName).toBe('Move-out bundle');
    expect(groups[0].lines.map((line) => line.id)).toEqual([
      'service-2',
      'service-3',
    ]);
    expect(groups[0].lines.every((line) => line.kind === 'bundled')).toBe(true);
    expect(groups[0].lines.every((line) => line.packageId === 'package-1')).toBe(
      true
    );

    expect(component.standaloneLines().map((line) => line.id)).toEqual([
      'service-1',
    ]);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Move-out bundle');
    expect(text).toContain('pages.order_management.refund.package_group');
    expect(text).toContain('pages.order_management.refund.line_kind.bundled');

    const checkboxes = fixture.debugElement.queryAll(By.directive(CheckboxStub));
    expect(checkboxes).toHaveLength(3);
    const labels = checkboxes.map(
      (c) => (c.componentInstance as CheckboxStub).label()
    );
    expect(labels).toContain('Window cleaning');
    expect(labels).toContain('Oven cleaning');
  });

  it('delegates submit to the facade with the order id', () => {
    setOrder(makeOrder({}));
    component.onSubmit();
    expect(facade.submit).toHaveBeenCalledWith('order-1', expect.any(Function));
  });

  it('emits refunded when the facade reports success', () => {
    setOrder(makeOrder({}));
    const emitted = jest.fn();
    component.refunded.subscribe(emitted);

    facade.submit.mockImplementation(
      (_orderId: string, onSuccess: () => void) => onSuccess()
    );
    component.onSubmit();

    expect(emitted).toHaveBeenCalledTimes(1);
  });
});
