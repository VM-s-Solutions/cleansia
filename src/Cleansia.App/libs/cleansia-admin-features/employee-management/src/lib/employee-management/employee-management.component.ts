import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminEmployeeListItem,
  ContractStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaMultiselectComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { EmployeeManagementFacade } from './employee-management.facade';
import { getEmployeeTableDefinition } from './employee-management.models';

@Component({
  selector: 'cleansia-admin-employee-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaMultiselectComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './employee-management.component.html',
  styleUrl: './employee-management.component.scss',
  providers: [EmployeeManagementFacade, DialogService],
})
export class EmployeeManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(EmployeeManagementFacade);
  private readonly translate = inject(TranslateService);

  contractStatusTemplate = viewChild<TemplateRef<any>>(
    'contractStatusTemplate'
  );

  employeeTableDefinition!: TableDefinition<AdminEmployeeListItem>;

  // Expose ContractStatus enum to template
  readonly ContractStatus = ContractStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  // Filter form
  filterForm = this.fb.group({
    contractStatus: [[] as ContractStatus[]],
    searchTerm: [''],
    isActive: [null as boolean | null],
  });

  // Contract status options for multiselect
  contractStatusMultiOptions = this.facade.contractStatusOptions;

  // Active status options - will be populated with translations
  activeStatusOptions: Array<{ label: string; value: boolean }> = [];

  ngAfterViewInit(): void {
    // Initialize active status options with translations
    this.activeStatusOptions = [
      { label: this.translate.instant('global.status.active'), value: true },
      { label: this.translate.instant('global.status.inactive'), value: false },
    ];

    this.employeeTableDefinition = getEmployeeTableDefinition(
      {
        onApprove: this.approveEmployee.bind(this),
        onReject: this.rejectEmployee.bind(this),
        onViewDetails: this.viewEmployeeDetails.bind(this),
      },
      this.translate,
      this.contractStatusTemplate()
    );

    this.cd.detectChanges();

    // Setup automatic filtering with debounce
    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Load employees on init
    this.facade.loadEmployees();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  approveEmployee(employee: AdminEmployeeListItem): void {
    this.facade.approveEmployee(employee.id!);
  }

  rejectEmployee(employee: AdminEmployeeListItem): void {
    this.facade.openRejectDialog(employee);
  }

  viewEmployeeDetails(employee: AdminEmployeeListItem): void {
    this.router.navigate(['/employee-management', employee.id]);
  }

  getContractStatusClass(employee: AdminEmployeeListItem): string {
    const statusName =
      employee.contractStatus?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `contract-status-badge status-${statusName}`;
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      contractStatuses:
        formValues.contractStatus && formValues.contractStatus.length > 0
          ? formValues.contractStatus
          : undefined,
      searchTerm: formValues.searchTerm?.trim() || undefined,
      isActive: formValues.isActive ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      contractStatus: [],
      searchTerm: '',
      isActive: null,
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    // Check if sort actually changed to prevent duplicate requests
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    // Update last sort state
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    const sort = [
      new SortDefinition({
        field: event.field,
        direction: sortDirection,
      }),
    ];
    this.facade.onSortChange(sort);
  }
}
