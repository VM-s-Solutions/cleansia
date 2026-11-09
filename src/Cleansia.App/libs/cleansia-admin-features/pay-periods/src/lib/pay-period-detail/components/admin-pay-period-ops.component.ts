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
  CleansiaTextareaComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { currentLanguage, formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AdminPayPeriodOpsFacade } from './admin-pay-period-ops.facade';
import { AdminPayPeriodOpsPanel } from './admin-pay-period-ops.models';

const DAY_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: 'cleansia-admin-pay-period-ops',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaTextareaComponent,
    CleansiaButtonComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './admin-pay-period-ops.component.html',
  providers: [AdminPayPeriodOpsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPayPeriodOpsComponent {
  protected readonly facade = inject(AdminPayPeriodOpsFacade);
  protected readonly Policy = Policy;
  private readonly lang = currentLanguage(inject(TranslateService));

  readonly payPeriod = input.required<PayPeriodDto>();
  readonly changed = output<void>();

  readonly isOpen = computed(
    () => this.payPeriod().status === PayPeriodStatus[PayPeriodStatus.Open]
  );

  readonly isClosed = computed(
    () => this.payPeriod().status === PayPeriodStatus[PayPeriodStatus.Closed]
  );

  // Close now's successor runs the configured length, which reaches the client only as this period's latest end.
  readonly closeNowHint = computed(() => {
    const payPeriod = this.payPeriod();
    return {
      end: formatDate(payPeriod.endDate, this.lang(), 'utcDate'),
      days:
        Math.round(
          (payPeriod.latestEndDate.getTime() - payPeriod.startDate.getTime()) /
            DAY_MS
        ) + 1,
    };
  });

  togglePanel(panel: AdminPayPeriodOpsPanel): void {
    this.facade.openPanel(panel);
  }

  onCloseNowReasonChange(value: string): void {
    this.facade.setCloseNowReason(value);
  }

  submitMarkPaid(): void {
    const payPeriodId = this.payPeriod().id;
    if (!payPeriodId) return;
    this.facade.markPaid(payPeriodId, () => this.changed.emit());
  }

  submitCloseNow(): void {
    const payPeriodId = this.payPeriod().id;
    if (!payPeriodId) return;
    this.facade.closeNow(payPeriodId, () => this.changed.emit());
  }
}
