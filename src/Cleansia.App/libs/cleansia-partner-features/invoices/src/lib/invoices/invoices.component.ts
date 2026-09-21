import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaHelpCardComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { EmployeeInvoiceDto } from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { InvoicesFacade } from './invoices.facade';
import { INVOICES_HELP_STEPS, INVOICE_STATUS_FLOW } from './invoices.helpers';
import { getInvoicesTableDefinition } from './invoices.models';

@Component({
  selector: 'cleansia-partner-invoices',
  standalone: true,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaButtonComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaSectionComponent,
    CleansiaCheckboxComponent,
    CleansiaTextInputComponent,
    CleansiaCalendarComponent,
    CleansiaHelpCardComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
  ],
  templateUrl: './invoices.component.html',
  providers: [InvoicesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoicesComponent {
  private readonly router = inject(Router);
  protected readonly facade = inject(InvoicesFacade);

  private readonly statusTemplate = viewChild<TemplateRef<EmployeeInvoiceDto>>('statusTemplate');
  private readonly invoicesHelpCard = viewChild<CleansiaHelpCardComponent>('invoicesHelpCard');

  protected readonly table = computed(() =>
    getInvoicesTableDefinition(
      { onDownload: (row) => this.facade.downloadInvoice(row) },
      this.facade.lang(),
      this.statusTemplate()
    )
  );

  invoicesHelpSteps = INVOICES_HELP_STEPS;
  invoiceStatusFlow = INVOICE_STATUS_FLOW;

  private readonly helpDismissedVersion = signal(0);
  readonly isInvoicesHelpDismissed = computed(() => {
    this.helpDismissedVersion();
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-invoices-help-dismissed');
  });

  viewInvoiceDetails(invoice: EmployeeInvoiceDto): void {
    if (!invoice.id) return;
    this.router.navigate([CleansiaPartnerRoute.INVOICES, invoice.id]);
  }

  onHelpDismissedChange(): void {
    this.helpDismissedVersion.update((v) => v + 1);
  }

  restoreHelp(): void {
    this.invoicesHelpCard()?.restore();
    this.helpDismissedVersion.update((v) => v + 1);
  }
}
