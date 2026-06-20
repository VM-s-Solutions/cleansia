/* Test doubles below intentionally mirror the real shared-component selectors
   and the PrimeNG-compatible `onClick` output so the override-imports swap is
   binding-compatible under the strict template test env. */
/* eslint-disable @angular-eslint/component-class-suffix */
/* eslint-disable @angular-eslint/no-output-on-prefix */
import { Component, forwardRef, input, output, signal, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { Subject } from 'rxjs';
import { PayPeriodDto, PayPeriodStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { AdminPayPeriodOpsComponent } from './admin-pay-period-ops.component';
import { AdminPayPeriodOpsFacade } from './admin-pay-period-ops.facade';
import { AdminPayPeriodOpsPanel } from './admin-pay-period-ops.models';

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
  readonly activePanel = signal<AdminPayPeriodOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);
  readonly reopenNotes = signal<string>('');
  openPanel = jest.fn((panel: AdminPayPeriodOpsPanel) =>
    this.activePanel.set(this.activePanel() === panel ? null : panel)
  );
  closePanel = jest.fn(() => this.activePanel.set(null));
  setReopenNotes = jest.fn();
  markPaid = jest.fn();
  reopen = jest.fn();
}

function makePayPeriod(partial: Partial<PayPeriodDto>): PayPeriodDto {
  return PayPeriodDto.fromJS({
    id: 'period-1',
    status: PayPeriodStatus[PayPeriodStatus.Closed],
    ...partial,
  });
}

describe('AdminPayPeriodOpsComponent', () => {
  let fixture: ComponentFixture<AdminPayPeriodOpsComponent>;
  let component: AdminPayPeriodOpsComponent;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [AdminPayPeriodOpsComponent, TranslateModule.forRoot()],
    })
      .overrideComponent(AdminPayPeriodOpsComponent, {
        remove: {
          imports: [
            CleansiaSectionComponent,
            CleansiaTextareaComponent,
            CleansiaButtonComponent,
          ],
        },
        add: {
          imports: [SectionStub, TextareaStub, ButtonStub],
          providers: [{ provide: AdminPayPeriodOpsFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(AdminPayPeriodOpsComponent);
    component = fixture.componentInstance;
  });

  function setPayPeriod(payPeriod: PayPeriodDto): void {
    fixture.componentRef.setInput('payPeriod', payPeriod);
    fixture.detectChanges();
  }

  function buttonLabels(): string[] {
    return fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((b) => (b.componentInstance as ButtonStub).label());
  }

  it('uses OnPush change detection', () => {
    const meta = (
      AdminPayPeriodOpsComponent as unknown as { ɵcmp: { onPush: boolean } }
    ).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('renders mark-paid and reopen actions for a closed period', () => {
    setPayPeriod(makePayPeriod({}));
    expect(buttonLabels()).toEqual(
      expect.arrayContaining([
        'pay_periods.detail.ops.mark_paid.action',
        'pay_periods.detail.ops.reopen.action',
      ])
    );
  });

  it('renders nothing for an open period', () => {
    setPayPeriod(
      makePayPeriod({ status: PayPeriodStatus[PayPeriodStatus.Open] })
    );
    expect(buttonLabels()).toEqual([]);
  });

  it('renders nothing for a paid period', () => {
    setPayPeriod(
      makePayPeriod({ status: PayPeriodStatus[PayPeriodStatus.Paid] })
    );
    expect(buttonLabels()).toEqual([]);
  });

  it('delegates submits with the pay period id', () => {
    setPayPeriod(makePayPeriod({}));

    component.submitMarkPaid();
    expect(facade.markPaid).toHaveBeenCalledWith(
      'period-1',
      expect.any(Function)
    );

    component.submitReopen();
    expect(facade.reopen).toHaveBeenCalledWith(
      'period-1',
      expect.any(Function)
    );
  });

  it('emits changed when an action reports success', () => {
    setPayPeriod(makePayPeriod({}));
    const emitted = jest.fn();
    component.changed.subscribe(emitted);

    facade.markPaid.mockImplementation(
      (_payPeriodId: string, onSuccess: () => void) => onSuccess()
    );
    component.submitMarkPaid();

    expect(emitted).toHaveBeenCalledTimes(1);
  });
});
