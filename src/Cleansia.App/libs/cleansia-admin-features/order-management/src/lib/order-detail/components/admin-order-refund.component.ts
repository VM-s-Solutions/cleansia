import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
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
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AdminOrderRefundFacade } from './admin-order-refund.facade';
import {
  REFUND_REASON_OPTIONS,
  RefundLineGroup,
  RefundLineOption,
} from './admin-order-refund.models';

@Component({
  selector: 'admin-order-refund',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaCheckboxComponent,
    CleansiaSelectComponent,
    CleansiaTextareaComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './admin-order-refund.component.html',
  providers: [AdminOrderRefundFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOrderRefundComponent {
  protected readonly facade = inject(AdminOrderRefundFacade);
  private readonly translate = inject(TranslateService);

  readonly order = input.required<OrderItem>();
  readonly refunded = output<void>();

  readonly isRefundable = computed(() => {
    const order = this.order();
    return (
      order.orderStatus?.value === OrderStatus.Completed &&
      order.paymentType?.value === PaymentType.Card
    );
  });

  readonly reasonOptions = computed<ICleansiaSelectOption[]>(() =>
    REFUND_REASON_OPTIONS.map((option) => ({
      label: this.translate.instant(option.labelKey),
      value: option.value,
    }))
  );

  readonly standaloneLines = computed<RefundLineOption[]>(() =>
    this.facade.lines().filter((line) => line.kind === 'service')
  );

  readonly packageGroups = computed<RefundLineGroup[]>(() => {
    const bundled = this.facade
      .lines()
      .filter(
        (line): line is RefundLineOption & { packageId: string } =>
          line.kind === 'bundled' && !!line.packageId
      );
    const groups = new Map<string, RefundLineGroup>();
    for (const line of bundled) {
      const group = groups.get(line.packageId);
      if (group) {
        group.lines.push(line);
      } else {
        groups.set(line.packageId, {
          packageId: line.packageId,
          packageName: this.packageNames().get(line.packageId) ?? '',
          lines: [line],
        });
      }
    }
    return [...groups.values()];
  });

  private readonly packageNames = computed<Map<string, string>>(() => {
    const names = new Map<string, string>();
    for (const pkg of this.order().selectedPackages ?? []) {
      if (pkg.id) {
        names.set(pkg.id, pkg.name ?? '');
      }
    }
    return names;
  });

  constructor() {
    effect(() => {
      const order = this.order();
      const serviceLines: RefundLineOption[] = (
        order.selectedServices ?? []
      ).map((service) => ({
        kind: 'service' as const,
        id: service.id ?? '',
        name: service.name ?? '',
        price: null,
        selected: false,
      }));

      const bundledLines: RefundLineOption[] = (
        order.selectedPackages ?? []
      ).flatMap((pkg) =>
        (pkg.includedServiceItems ?? []).map((item) => ({
          kind: 'bundled' as const,
          id: item.id ?? '',
          name: item.name ?? '',
          price: null,
          selected: false,
          packageId: pkg.id ?? '',
        }))
      );

      const lines = [...serviceLines, ...bundledLines].filter(
        (line) => !!line.id && (line.kind !== 'bundled' || !!line.packageId)
      );
      this.facade.setLines(lines);
    });
  }

  onLineToggle(id: string, selected: boolean): void {
    this.facade.toggleLine(id, selected);
  }

  onReasonChange(reason: RefundReason | null): void {
    this.facade.setReason(reason);
  }

  onOverrideReasonChange(value: string): void {
    this.facade.setOverrideReason(value);
  }

  onSubmit(): void {
    const orderId = this.order().id;
    if (!orderId) return;
    this.facade.submit(orderId, () => this.refunded.emit());
  }
}
