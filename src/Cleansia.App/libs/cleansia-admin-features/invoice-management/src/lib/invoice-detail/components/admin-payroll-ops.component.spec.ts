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
import {
  EmployeeInvoiceDetailDto,
  EmployeeInvoiceStatus,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { AdminPayrollOpsComponent } from './admin-payroll-ops.component';
import { AdminPayrollOpsFacade } from './admin-payroll-ops.facade';
import { AdminPayrollOpsPanel } from './admin-payroll-ops.models';

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
  selector: 'cleansia-text-input',
  standalone: true,
  template: '',
  providers: [valueAccessor(() => TextInputStub)],
})
class TextInputStub extends ControlStub {
  label = input<string>('');
  placeholder = input<string>('');
  dataType = input<string>('text');
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
  readonly activePanel = signal<AdminPayrollOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);
  readonly bonusAmount = signal<string>('');
  readonly deductionAmount = signal<string>('');
  readonly adjustNotes = signal<string>('');
  readonly disputeNotes = signal<string>('');
  readonly rejectNotes = signal<string>('');
  readonly canSubmitAdjust = signal<boolean>(false);
  readonly canSubmitDispute = signal<boolean>(false);
  readonly canSubmitReject = signal<boolean>(false);
  openPanel = jest.fn((panel: AdminPayrollOpsPanel) =>
    this.activePanel.set(this.activePanel() === panel ? null : panel)
  );
  openAdjustPanel = jest.fn(() =>
    this.activePanel.set(this.activePanel() === 'adjust' ? null : 'adjust')
  );
  closePanel = jest.fn(() => this.activePanel.set(null));
  setBonusAmount = jest.fn();
  setDeductionAmount = jest.fn();
  setAdjustNotes = jest.fn();
  setDisputeNotes = jest.fn();
  setRejectNotes = jest.fn();
  adjustAmounts = jest.fn();
  disputeInvoice = jest.fn();
  rejectInvoice = jest.fn();
}

function makeInvoice(
  partial: Partial<EmployeeInvoiceDetailDto>
): EmployeeInvoiceDetailDto {
  return EmployeeInvoiceDetailDto.fromJS({
    id: 'invoice-1',
    status: EmployeeInvoiceStatus.Pending,
    bonusAmount: 100,
    deductionAmount: 20,
    ...partial,
  });
}

describe('AdminPayrollOpsComponent', () => {
  let fixture: ComponentFixture<AdminPayrollOpsComponent>;
  let component: AdminPayrollOpsComponent;
  let facade: FacadeStub;

  beforeEach(async () => {
    facade = new FacadeStub();

    await TestBed.configureTestingModule({
      imports: [AdminPayrollOpsComponent, TranslateModule.forRoot()],
    })
      .overrideComponent(AdminPayrollOpsComponent, {
        remove: {
          imports: [
            CleansiaSectionComponent,
            CleansiaTextInputComponent,
            CleansiaTextareaComponent,
            CleansiaButtonComponent,
          ],
        },
        add: {
          imports: [SectionStub, TextInputStub, TextareaStub, ButtonStub],
          providers: [{ provide: AdminPayrollOpsFacade, useValue: facade }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(AdminPayrollOpsComponent);
    component = fixture.componentInstance;
  });

  function setInvoice(invoice: EmployeeInvoiceDetailDto): void {
    fixture.componentRef.setInput('invoice', invoice);
    fixture.detectChanges();
  }

  function buttonLabels(): string[] {
    return fixture.debugElement
      .queryAll(By.directive(ButtonStub))
      .map((b) => (b.componentInstance as ButtonStub).label());
  }

  it('uses OnPush change detection', () => {
    const meta = (
      AdminPayrollOpsComponent as unknown as { ɵcmp: { onPush: boolean } }
    ).ɵcmp;
    expect(meta.onPush).toBe(true);
  });

  it('renders adjust, dispute and reject actions for a pending invoice', () => {
    setInvoice(makeInvoice({ status: EmployeeInvoiceStatus.Pending }));
    expect(buttonLabels()).toEqual(
      expect.arrayContaining([
        'pages.invoice_detail.ops.adjust.action',
        'pages.invoice_detail.ops.dispute.action',
        'pages.invoice_detail.ops.reject.action',
      ])
    );
  });

  it('renders nothing for a paid invoice', () => {
    setInvoice(makeInvoice({ status: EmployeeInvoiceStatus.Paid }));
    expect(buttonLabels()).toEqual([]);
  });

  it('hides the adjust action when the invoice is approved', () => {
    setInvoice(makeInvoice({ status: EmployeeInvoiceStatus.Approved }));
    const labels = buttonLabels();
    expect(labels).not.toContain('pages.invoice_detail.ops.adjust.action');
    expect(labels).toContain('pages.invoice_detail.ops.dispute.action');
  });

  it('hides the dispute action when the invoice is already disputed', () => {
    setInvoice(makeInvoice({ status: EmployeeInvoiceStatus.Disputed }));
    const labels = buttonLabels();
    expect(labels).not.toContain('pages.invoice_detail.ops.dispute.action');
    expect(labels).toContain('pages.invoice_detail.ops.adjust.action');
  });

  it('hides the reject action when the invoice is already rejected', () => {
    setInvoice(makeInvoice({ status: EmployeeInvoiceStatus.Rejected }));
    expect(buttonLabels()).not.toContain(
      'pages.invoice_detail.ops.reject.action'
    );
  });

  it('seeds the adjust panel from the current invoice amounts', () => {
    setInvoice(makeInvoice({ bonusAmount: 100, deductionAmount: 20 }));
    component.toggleAdjustPanel();
    expect(facade.openAdjustPanel).toHaveBeenCalledWith(100, 20);
  });

  it('delegates submits with the invoice id', () => {
    setInvoice(makeInvoice({}));

    component.submitAdjust();
    expect(facade.adjustAmounts).toHaveBeenCalledWith(
      'invoice-1',
      expect.any(Function)
    );

    component.submitDispute();
    expect(facade.disputeInvoice).toHaveBeenCalledWith(
      'invoice-1',
      expect.any(Function)
    );

    component.submitReject();
    expect(facade.rejectInvoice).toHaveBeenCalledWith(
      'invoice-1',
      expect.any(Function)
    );
  });

  it('emits changed when an action reports success', () => {
    setInvoice(makeInvoice({}));
    const emitted = jest.fn();
    component.changed.subscribe(emitted);

    facade.disputeInvoice.mockImplementation(
      (_invoiceId: string, onSuccess: () => void) => onSuccess()
    );
    component.submitDispute();

    expect(emitted).toHaveBeenCalledTimes(1);
  });
});
