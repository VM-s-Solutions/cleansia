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
import { OrderItem, OrderStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AdminOrderOpsFacade } from './admin-order-ops.facade';
import {
  AdminOrderOpsPanel,
  OVERRIDE_STATUS_OPTIONS,
} from './admin-order-ops.models';

@Component({
  selector: 'admin-order-ops',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './admin-order-ops.component.html',
  providers: [AdminOrderOpsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOrderOpsComponent {
  protected readonly facade = inject(AdminOrderOpsFacade);
  private readonly translate = inject(TranslateService);

  protected readonly OrderStatus = OrderStatus;

  readonly order = input.required<OrderItem>();
  readonly changed = output<void>();

  readonly statusOptions = computed<ICleansiaSelectOption[]>(() =>
    OVERRIDE_STATUS_OPTIONS.map((option) => ({
      label: this.translate.instant(option.labelKey),
      value: option.value,
    }))
  );

  readonly fromEmployeeOptions = computed<ICleansiaSelectOption[]>(() =>
    (this.order().assignedEmployees ?? [])
      .filter((employee) => !!employee.employeeId)
      .map((employee) => ({
        label:
          employee.fullName ||
          this.translate.instant('pages.order_management.ops.reassign.unnamed'),
        value: employee.employeeId,
      }))
  );

  readonly hasAssignedEmployees = computed(
    () => this.fromEmployeeOptions().length > 0
  );

  togglePanel(panel: AdminOrderOpsPanel): void {
    this.facade.openPanel(panel);
  }

  onCancelReasonChange(value: string): void {
    this.facade.setCancelReason(value);
  }

  onTargetStatusChange(value: OrderStatus | null): void {
    this.facade.setTargetStatus(value);
  }

  onFromEmployeeChange(value: string | null): void {
    this.facade.setFromEmployeeId(value);
  }

  onToEmployeeChange(value: string): void {
    this.facade.setToEmployeeId(value);
  }

  submitCancel(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    this.facade.cancelOrder(orderId, () => this.changed.emit());
  }

  submitOverrideStatus(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    this.facade.overrideStatus(orderId, () => this.changed.emit());
  }

  submitReassign(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    this.facade.reassignOrder(orderId, () => this.changed.emit());
  }

  submitRefund(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    this.facade.refundOrder(orderId, () => this.changed.emit());
  }
}
