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
import { PayPeriodDto, PayPeriodStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { AdminPayPeriodOpsFacade } from './admin-pay-period-ops.facade';
import { AdminPayPeriodOpsPanel } from './admin-pay-period-ops.models';

@Component({
  selector: 'cleansia-admin-pay-period-ops',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextareaComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './admin-pay-period-ops.component.html',
  providers: [AdminPayPeriodOpsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPayPeriodOpsComponent {
  protected readonly facade = inject(AdminPayPeriodOpsFacade);

  readonly payPeriod = input.required<PayPeriodDto>();
  readonly changed = output<void>();

  readonly isClosed = computed(
    () => this.payPeriod().status === PayPeriodStatus[PayPeriodStatus.Closed]
  );

  togglePanel(panel: AdminPayPeriodOpsPanel): void {
    this.facade.openPanel(panel);
  }

  onReopenNotesChange(value: string): void {
    this.facade.setReopenNotes(value);
  }

  submitMarkPaid(): void {
    const payPeriodId = this.payPeriod().id;
    if (!payPeriodId) return;
    this.facade.markPaid(payPeriodId, () => this.changed.emit());
  }

  submitReopen(): void {
    const payPeriodId = this.payPeriod().id;
    if (!payPeriodId) return;
    this.facade.reopen(payPeriodId, () => this.changed.emit());
  }
}
