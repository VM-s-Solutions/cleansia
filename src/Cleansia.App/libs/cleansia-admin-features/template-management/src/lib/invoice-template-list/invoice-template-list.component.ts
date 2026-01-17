import { ChangeDetectorRef, Component, OnDestroy, OnInit, TemplateRef, inject, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  TableColumn,
  TableAction,
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
})
export class InvoiceTemplateListComponent implements OnInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  readonly facade = inject(InvoiceTemplateListFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  private destroy$ = new Subject<void>();

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  columns!: TableColumn<InvoiceTemplateListItem>[];
  actions!: TableAction<InvoiceTemplateListItem>[];

  ngOnInit(): void {
    this.rebuildTableDefinitions();
    this.facade.loadTemplates();

    // Rebuild tables when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
      });
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getInvoiceTemplateTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEdit(row),
        onActivate: (row) => this.onActivate(row),
        onDeactivate: (row) => this.onDeactivate(row),
        onDelete: (row) => this.onDelete(row),
      },
      this.translate,
      this.statusTemplate()
    );
    this.columns = tableDef.columns;
    this.actions = tableDef.actions;
    this.cd.detectChanges();
  }

  getActiveStatusLabel(template: InvoiceTemplateListItem): string {
    return template.isActive
      ? this.translate.instant('global.status.active')
      : this.translate.instant('global.status.inactive');
  }

  getActiveStatusClass(template: InvoiceTemplateListItem): string {
    return template.isActive
      ? 'active-status-badge status-active'
      : 'active-status-badge status-inactive';
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
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }
}
