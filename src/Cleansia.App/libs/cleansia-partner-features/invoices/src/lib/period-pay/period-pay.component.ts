import { ChangeDetectionStrategy, Component, OnInit, inject, computed } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { PeriodPayFacade } from './period-pay.facade';
import { getPeriodPayTableDefinition } from './period-pay.models';

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
  // Computed, not a field initializer: the currency arrives with the summary, so the column formatters
  // have to be rebuilt when it does or every row would render with whatever was known at construction.
  protected readonly periodPayColumns = computed(
    () => getPeriodPayTableDefinition(this.facade.summary()?.currencyCode).columns
  );

  ngOnInit(): void {
    this.facade.connectPeriodControl(this.periodControl);
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
