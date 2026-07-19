import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Subject, takeUntil } from 'rxjs';
import { EmployeePayConfigDto } from '@cleansia/admin-services';
import { PayConfigManagementFacade } from './pay-config-management.facade';
import { getPayConfigTableDefinition } from './pay-config-management.models';

@Component({
  selector: 'cleansia-admin-pay-config-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    ConfirmDialogModule,
  ],
  templateUrl: './pay-config-management.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PayConfigManagementFacade, ConfirmationService],
})
export class PayConfigManagementComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  protected readonly facade = inject(PayConfigManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  payConfigColumns!: TableColumn<EmployeePayConfigDto>[];
  payConfigActions!: TableAction<EmployeePayConfigDto>[];

  private destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
      });

    this.facade.loadPayConfigs();
  }

  private rebuildTableDefinitions(): void {
    const tableDefinition = getPayConfigTableDefinition(
      {
        onEdit: this.editPayConfig.bind(this),
        onDelete: this.confirmDelete.bind(this),
      },
      this.translate,
      this.facade.formatCurrency.bind(this.facade)
    );

    this.payConfigColumns = tableDefinition.columns;
    this.payConfigActions = tableDefinition.actions;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  createPayConfig(): void {
    this.facade.navigateToCreate();
  }

  editPayConfig(payConfig: EmployeePayConfigDto): void {
    this.facade.navigateToEdit(payConfig);
  }

  confirmDelete(payConfig: EmployeePayConfigDto): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.pay_config_management.delete_confirm'),
      header: this.translate.instant('pages.pay_config_management.delete'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deletePayConfig(payConfig);
      },
    });
  }
}
