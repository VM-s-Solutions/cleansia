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
import { OrderItem, OrderStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { AdminOrderOpsComponent } from './admin-order-ops.component';
import { AdminOrderOpsFacade } from './admin-order-ops.facade';
import { AdminOrderOpsPanel } from './admin-order-ops.models';

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
  selector: 'cleansia-text-input',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextInputStub)],
})
class TextInputStub extends ControlStub {
  label = input<string>('');
  placeholder = input<string>('');
  valueChanges = output<string>();
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
  outlined = input<boolean>(false);
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
  readonly activePanel = signal<AdminOrderOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);
  readonly cancelReason = signal<string>('');
  readonly targetStatus = signal<OrderStatus | null>(null);
  readonly fromEmployeeId = signal<string | null>(null);
  readonly toEmployeeId = signal<string>('');
  readonly canSubmitOverrideStatus = signal<boolean>(false);
  readonly canSubmitReassign = signal<boolean>(false);
  openPanel = jest.fn((panel: AdminOrderOpsPanel) =>
    this.activePanel.set(this.activePanel() === panel ? null : panel)
  );
  closePanel = jest.fn(() => this.activePanel.set(null));
  setCancelReason = jest.fn();
  setTargetStatus = jest.fn();
  setFromEmployeeId = jest.fn();
  setToEmployeeId = jest.fn();
  cancelOrder = jest.fn();
  overrideStatus = jest.fn();
  reassignOrder = jest.fn();
  refundOrder = jest.fn();
}

function makeOrder(partial: Partial<OrderItem>): OrderItem {
  return OrderItem.fromJS({
    id: 'order-1',
    orderStatus: { value: OrderStatus.Confirmed },
    assignedEmployees: [
      { id: 'oe-1', employeeId: 'employee-1', fullName: 'Jane Cleaner' },
    ],
    ...partial,
  });
}

describe('AdminOrderOpsComponent', () => {
  let fixture: ComponentFixture<AdminOrderOpsComponent>;
  let component: AdminOrderOpsComponent;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [AdminOrderOpsComponent, TranslateModule.forRoot()],
    })
      .overrideComponent(AdminOrderOpsComponent, {
        remove: {
          imports: [
            CleansiaSectionComponent,
            CleansiaSelectComponent,
            CleansiaTextInputComponent,
            CleansiaTextareaComponent,
            CleansiaButtonComponent,
          ],
        },
        add: {
          imports: [
            SectionStub,
            SelectStub,
            TextInputStub,
            TextareaStub,
            ButtonStub,
          ],
          providers: [{ provide: AdminOrderOpsFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(AdminOrderOpsComponent);
    component = fixture.componentInstance;
  });

  function setOrder(order: OrderItem): void {
    fixture.componentRef.setInput('order', order);
    fixture.detectChanges();
  }

  it('uses OnPush change detection', () => {
    const meta = (
      AdminOrderOpsComponent as unknown as { ɵcmp: { onPush: boolean } }
    ).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('renders the four action buttons', () => {
    setOrder(makeOrder({}));
    const buttons = fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((b) => (b.componentInstance as ButtonStub).label());
    expect(buttons).toEqual(
      expect.arrayContaining([
        'pages.order_management.ops.cancel.action',
        'pages.order_management.ops.override_status.action',
        'pages.order_management.ops.reassign.action',
        'pages.order_management.ops.refund.action',
      ])
    );
  });

  it('opens the cancel panel and delegates submit with the order id', () => {
    setOrder(makeOrder({}));
    component.togglePanel('cancel');
    expect(facade.openPanel).toHaveBeenCalledWith('cancel');
    fixture.detectChanges();

    component.submitCancel();
    expect(facade.cancelOrder).toHaveBeenCalledWith(
      'order-1',
      expect.any(Function)
    );
  });

  it('exposes the seven order status options for the override select', () => {
    setOrder(makeOrder({}));
    expect(component.statusOptions()).toHaveLength(7);
    expect(component.statusOptions().map((o) => o.value)).toEqual([
      OrderStatus.New,
      OrderStatus.Pending,
      OrderStatus.Confirmed,
      OrderStatus.OnTheWay,
      OrderStatus.InProgress,
      OrderStatus.Completed,
      OrderStatus.Cancelled,
    ]);
  });

  it('derives from-employee options from the assigned employees', () => {
    setOrder(makeOrder({}));
    expect(component.fromEmployeeOptions()).toEqual([
      { label: 'Jane Cleaner', value: 'employee-1' },
    ]);
    expect(component.hasAssignedEmployees()).toBe(true);
  });

  it('reports no assigned employees when the order has none', () => {
    setOrder(makeOrder({ assignedEmployees: [] }));
    expect(component.hasAssignedEmployees()).toBe(false);
  });

  it('emits changed when an action reports success', () => {
    setOrder(makeOrder({}));
    const emitted = jest.fn();
    component.changed.subscribe(emitted);

    facade.refundOrder.mockImplementation(
      (_orderId: string, onSuccess: () => void) => onSuccess()
    );
    component.submitRefund();

    expect(emitted).toHaveBeenCalledTimes(1);
  });
});
