import { ChangeDetectionStrategy, Component, inject, input, OnInit, output } from '@angular/core';
import { CleansiaButtonComponent } from '@cleansia/components';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { AmountDueFacade } from './amount-due.facade';
import { AmountDueRow } from './amount-due.models';

@Component({
  selector: 'cleansia-customer-amount-due',
  standalone: true,
  imports: [TranslatePipe, SkeletonModule, CleansiaButtonComponent],
  templateUrl: './amount-due.component.html',
  providers: [AmountDueFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AmountDueComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(AmountDueFacade);

  /** One order's amounts, on that order's page; unset lists everything owed. */
  readonly orderId = input<string | null>(null);
  /** Emitted just before the browser leaves for the pay link. */
  readonly leaving = output<void>();

  ngOnInit(): void {
    this.facade.init(this.orderId());
  }

  pay(row: AmountDueRow): void {
    this.facade.pay(row.id, () => this.leaving.emit());
  }

  formatAmount(row: AmountDueRow): string {
    return formatMoney(row.amount, row.currencyCode, localeFor(this.translate.currentLang));
  }
}
