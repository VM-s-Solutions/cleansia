import { ChangeDetectionStrategy, Component, OnInit, inject, computed } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { PeriodPayFacade } from './period-pay.facade';
import { formatPayAmount, getPeriodPayTableDefinition } from './period-pay.models';

@Component({
  selector: 'cleansia-partner-period-pay',
  standalone: true,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './period-pay.component.html',
  providers: [PeriodPayFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PeriodPayComponent implements OnInit {
  protected readonly facade = inject(PeriodPayFacade);
  private readonly router = inject(Router);

  protected readonly periodControl = new FormControl<string | null>(null);
  protected readonly currencyControl = new FormControl<string | null>(null);
  // Computed, not a field initializer: the currency arrives with the summary, so the column formatters
  // have to be rebuilt when it does or every row would render with whatever was known at construction.
  protected readonly periodPayColumns = computed(
    () => getPeriodPayTableDefinition(this.facade.summary()?.currencyCode, this.facade.lang()).columns
  );

  protected readonly amounts = computed(() => {
    const summary = this.facade.summary();
    const lang = this.facade.lang();
    const amount = (value: number | undefined): string =>
      formatPayAmount(value, summary?.currencyCode, lang);
    return {
      base: amount(summary?.totalBasePay),
      extras: amount(summary?.totalExtrasPay),
      expenses: amount(summary?.totalExpensesPay),
      bonus: amount(summary?.totalBonusPay),
      deduction: amount(summary?.totalDeductionPay),
      grandTotal: amount(summary?.grandTotal),
    };
  });

  ngOnInit(): void {
    this.facade.connectPeriodControl(this.periodControl);
    this.facade.connectCurrencyControl(this.currencyControl);
    this.facade.init();
  }

  retry(): void {
    this.facade.retry();
  }

  viewInvoice(): void {
    const invoiceId = this.facade.summary()?.invoiceId;
    if (invoiceId) {
      this.router.navigate([CleansiaPartnerRoute.INVOICES, invoiceId]);
    }
  }
}
