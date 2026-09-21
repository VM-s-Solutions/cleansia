import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
} from '@cleansia/components';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { currentLanguage, formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InvoiceDetailFacade } from './invoice-detail.facade';
import { formatInvoiceAmount, getOrderPaysTableDefinition } from './invoice-detail.models';

@Component({
  selector: 'cleansia-partner-invoice-detail',
  standalone: true,
  imports: [
    CleansiaLoaderComponent,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
  ],
  templateUrl: './invoice-detail.component.html',
  providers: [InvoiceDetailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDetailComponent implements OnInit {
  protected readonly facade = inject(InvoiceDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly invoiceDetail = this.facade.invoiceDetail;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.error;

  private readonly lang = currentLanguage(this.translate);

  protected readonly orderPaysColumns = computed(
    () => getOrderPaysTableDefinition(this.invoiceDetail()?.currencyCode, this.lang()).columns
  );

  protected readonly dates = computed(() => {
    const invoice = this.invoiceDetail();
    const lang = this.lang();
    return {
      generated: formatDate(invoice?.generatedAt, lang, 'dateTime'),
      approved: formatDate(invoice?.approvedAt, lang, 'dateTime'),
      paid: formatDate(invoice?.paidAt, lang, 'dateTime'),
    };
  });

  protected readonly amounts = computed(() => {
    const invoice = this.invoiceDetail();
    const lang = this.lang();
    const amount = (value: number | undefined): string =>
      formatInvoiceAmount(value, invoice?.currencyCode, lang);
    return {
      subTotal: amount(invoice?.subTotal),
      bonus: amount(invoice?.bonusAmount),
      deduction: amount(invoice?.deductionAmount),
      total: amount(invoice?.totalAmount),
    };
  });

  ngOnInit(): void {
    const invoiceId = this.route.snapshot.paramMap.get('invoiceId');
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    } else {
      this.navigateToInvoices();
    }
  }

  navigateToInvoices(): void {
    this.router.navigate([CleansiaPartnerRoute.INVOICES]);
  }

  retryLoadInvoice(): void {
    const invoiceId = this.route.snapshot.paramMap.get('invoiceId');
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    }
  }

  downloadPdf(): void {
    this.facade.downloadPdf();
  }

  printInvoice(): void {
    this.facade.printInvoice();
  }
}
