import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminEmployeeListItem, ContractStatus } from '@cleansia/admin-services';
import {
  CleansiaCheckboxComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaRadioComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute, PermissionService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { EmployeeManagementFacade } from './employee-management.facade';
import { getEmployeeTableDefinition } from './employee-management.models';

@Component({
  selector: 'cleansia-admin-employee-management',
  standalone: true,
  imports: [
    CleansiaCheckboxComponent,
    CleansiaRadioComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './employee-management.component.html',
  providers: [EmployeeManagementFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeManagementComponent implements OnInit {
  private readonly router = inject(Router);
  protected readonly facade = inject(EmployeeManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  private readonly contractStatusTemplate = viewChild<TemplateRef<AdminEmployeeListItem>>(
    'contractStatusTemplate'
  );

  readonly ContractStatus = ContractStatus;

  protected readonly table = computed(() => {
    this.facade.lang();
    return getEmployeeTableDefinition(
      {
        onApprove: (row) => this.facade.openApproveDialog(row),
        onReject: (row) => this.facade.openRejectDialog(row),
        onViewDetails: (row) => this.viewEmployeeDetails(row),
      },
      this.translate,
      this.permissions,
      this.contractStatusTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadEmployees();
  }

  viewEmployeeDetails(employee: AdminEmployeeListItem): void {
    this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT, employee.id]);
  }

  toggleContractStatus(status: ContractStatus): void {
    this.facade.setContractStatus(status, !this.facade.isContractStatusChecked(status));
  }
}
