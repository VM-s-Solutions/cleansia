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
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { EmployeePayConfigDto } from '@cleansia/admin-services';
import { PayConfigManagementFacade } from './pay-config-management.facade';
import { getPayConfigTableDefinition } from './pay-config-management.models';

@Component({
  selector: 'cleansia-admin-pay-config-management',
  standalone: true,
  imports: [
    CleansiaPermissionDirective,
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
  ],
  templateUrl: './pay-config-management.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PayConfigManagementFacade],
})
export class PayConfigManagementComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  protected readonly facade = inject(PayConfigManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

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
      this.permissions,
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
    this.facade.deletePayConfig(payConfig);
  }
}
