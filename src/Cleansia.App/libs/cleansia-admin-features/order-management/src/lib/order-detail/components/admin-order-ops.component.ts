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
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePickerModule } from 'primeng/datepicker';
import { FloatLabelModule } from 'primeng/floatlabel';
import { AdminOrderOpsFacade } from './admin-order-ops.facade';
import {
  AdminOrderOpsPanel,
  isLockoutConfirmable,
  OVERRIDE_STATUS_OPTIONS,
} from './admin-order-ops.models';

@Component({
  selector: 'cleansia-admin-order-ops',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaButtonComponent,
    DatePickerModule,
    FloatLabelModule,
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

  readonly canConfirmLockout = computed(() => isLockoutConfirmable(this.order()));

  togglePanel(panel: AdminOrderOpsPanel): void {
    this.facade.openPanel(panel);
  }

  onCancelReasonChange(value: string): void {
    this.facade.setCancelReason(value);
  }

  onTargetStatusChange(value: OrderStatus | null): void {
    this.facade.setTargetStatus(value);
  }

  onOverrideReasonChange(value: string): void {
    this.facade.setOverrideReason(value);
  }

  onFromEmployeeChange(value: string | null): void {
    this.facade.setFromEmployeeId(value);
  }

  onToEmployeeChange(value: string): void {
    this.facade.setToEmployeeId(value);
  }

  onRemovalReasonChange(value: string): void {
    this.facade.setRemovalReason(value);
  }

  onCashEmployeeChange(value: string | null): void {
    this.facade.setCashEmployeeId(value);
  }

  onCashReceivedAtChange(value: Date | null): void {
    this.facade.setCashReceivedAt(value);
  }

  onCashAmountChange(value: string): void {
    this.facade.setCashAmount(value);
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

  submitNoShow(): void {
    const order = this.order();
    if (!order.id) return;
    this.facade.cancelAsNoShow(order.id, order.currency?.code, () => this.changed.emit());
  }

  submitLockout(): void {
    const order = this.order();
    if (!order.id) return;
    this.facade.cancelAsLockout(order.id, order.currency?.code, () => this.changed.emit());
  }

  submitRecordCash(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    this.facade.recordCashReceived(orderId, () => this.changed.emit());
  }
}
