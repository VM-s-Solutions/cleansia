import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  TableDefinition,
} from '@cleansia/components';
import { InvoiceTemplateListItem } from '@cleansia/admin-services';
import { InvoiceTemplateListFacade } from './invoice-template-list.facade';
import { getInvoiceTemplateTableDefinition } from './invoice-template-list.models';

@Component({
  selector: 'lib-invoice-template-list',
  standalone: true,
  imports: [
    CommonModule,
    TranslateModule,
    ConfirmDialogModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
  ],
  providers: [InvoiceTemplateListFacade, ConfirmationService],
  templateUrl: './invoice-template-list.component.html',
  styleUrl: './invoice-template-list.component.scss',
})
export class InvoiceTemplateListComponent implements OnInit, OnDestroy {
  readonly facade = inject(InvoiceTemplateListFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  tableDefinition!: TableDefinition<InvoiceTemplateListItem>;

  ngOnInit(): void {
    this.tableDefinition = getInvoiceTemplateTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEdit(row),
        onActivate: (row) => this.onActivate(row),
        onDeactivate: (row) => this.onDeactivate(row),
        onDelete: (row) => this.onDelete(row),
      },
      this.translate
    );
    this.facade.loadTemplates();
  }

  onActivate(template: InvoiceTemplateListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.template_management.confirm_activate'),
      header: this.translate.instant('pages.template_management.activate_template'),
      icon: 'pi pi-check',
      accept: () => {
        this.facade.activateTemplate(template);
      },
    });
  }

  onDeactivate(template: InvoiceTemplateListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.template_management.confirm_deactivate'),
      header: this.translate.instant('pages.template_management.deactivate_template'),
      icon: 'pi pi-times',
      accept: () => {
        this.facade.deactivateTemplate(template);
      },
    });
  }

  onDelete(template: InvoiceTemplateListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.template_management.confirm_delete'),
      header: this.translate.instant('pages.template_management.delete_template'),
      icon: 'pi pi-trash',
      accept: () => {
        this.facade.deleteTemplate(template);
      },
    });
  }

  onSortChange(event: { field: string; order: number }): void {
    this.facade.onSortChange(event);
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }
}
