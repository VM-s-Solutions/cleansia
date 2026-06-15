import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
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
import { TranslatePipe } from '@ngx-translate/core';
import { AdminPayrollOpsFacade } from './admin-payroll-ops.facade';
import { AdminPayrollOpsPanel } from './admin-payroll-ops.models';

@Component({
  selector: 'cleansia-admin-payroll-ops',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './admin-payroll-ops.component.html',
  providers: [AdminPayrollOpsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPayrollOpsComponent {
  protected readonly facade = inject(AdminPayrollOpsFacade);

  readonly invoice = input.required<EmployeeInvoiceDetailDto>();
  readonly changed = output<void>();

  readonly canAdjust = computed(() => {
    const status = this.invoice().status;
    return (
      status === EmployeeInvoiceStatus.Pending ||
      status === EmployeeInvoiceStatus.Disputed
    );
  });

  readonly canDispute = computed(() => {
    const status = this.invoice().status;
    return (
      status !== EmployeeInvoiceStatus.Paid &&
      status !== EmployeeInvoiceStatus.Disputed &&
      status !== EmployeeInvoiceStatus.Cancelled
    );
  });

  readonly canReject = computed(() => {
    const status = this.invoice().status;
    return (
      status !== EmployeeInvoiceStatus.Paid &&
      status !== EmployeeInvoiceStatus.Rejected &&
      status !== EmployeeInvoiceStatus.Cancelled
    );
  });

  readonly hasAnyAction = computed(
    () => this.canAdjust() || this.canDispute() || this.canReject()
  );

  togglePanel(panel: AdminPayrollOpsPanel): void {
    this.facade.openPanel(panel);
  }

  toggleAdjustPanel(): void {
    const invoice = this.invoice();
    this.facade.openAdjustPanel(
      invoice.bonusAmount ?? 0,
      invoice.deductionAmount ?? 0
    );
  }

  onBonusAmountChange(value: string): void {
    this.facade.setBonusAmount(value);
  }

  onDeductionAmountChange(value: string): void {
    this.facade.setDeductionAmount(value);
  }

  onAdjustNotesChange(value: string): void {
    this.facade.setAdjustNotes(value);
  }

  onDisputeNotesChange(value: string): void {
    this.facade.setDisputeNotes(value);
  }

  onRejectNotesChange(value: string): void {
    this.facade.setRejectNotes(value);
  }

  submitAdjust(): void {
    const invoiceId = this.invoice().id;
    if (!invoiceId) return;
    this.facade.adjustAmounts(invoiceId, () => this.changed.emit());
  }

  submitDispute(): void {
    const invoiceId = this.invoice().id;
    if (!invoiceId) return;
    this.facade.disputeInvoice(invoiceId, () => this.changed.emit());
  }

  submitReject(): void {
    const invoiceId = this.invoice().id;
    if (!invoiceId) return;
    this.facade.rejectInvoice(invoiceId, () => this.changed.emit());
  }
}
