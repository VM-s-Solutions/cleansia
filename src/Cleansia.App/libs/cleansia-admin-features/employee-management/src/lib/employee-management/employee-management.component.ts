import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminEmployeeListItem,
  ContractStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaRadioComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
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
    CleansiaCheckboxComponent,
    CleansiaRadioComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './employee-management.component.html',
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

  employeeColumns!: TableColumn<AdminEmployeeListItem>[];
  employeeActions!: TableAction<AdminEmployeeListItem>[];

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

  // Contract status options for multiselect - will be rebuilt on language change
  contractStatusMultiOptions: { label: string; value: ContractStatus }[] = [];

  // Active status options - will be populated with translations
  activeStatusOptions: Array<{ label: string; value: boolean }> = [];

  // Filter drawer state
  isFilterDrawerOpen = signal(false);
  // Signal to trigger recalculation of filter chips when form changes
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    // Access the signal to create dependency
    this.filterFormVersion();
    return this.getActiveFilterChips();
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    // Update filter chips immediately when form changes
    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    // Setup automatic filtering with debounce
    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Rebuild tables and filters when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      });

    // Load employees on init
    this.facade.loadEmployees();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getEmployeeTableDefinition(
      {
        onApprove: this.approveEmployee.bind(this),
        onReject: this.rejectEmployee.bind(this),
        onViewDetails: this.viewEmployeeDetails.bind(this),
      },
      this.translate,
      this.contractStatusTemplate()
    );

    this.employeeColumns = tableDef.columns;
    this.employeeActions = tableDef.actions;
  }

  private rebuildFilterOptions(): void {
    this.contractStatusMultiOptions = [
      {
        label: this.translate.instant('pages.employee_management.contract_status.pending'),
        value: ContractStatus.Pending,
      },
      {
        label: this.translate.instant('pages.employee_management.contract_status.active'),
        value: ContractStatus.Active,
      },
      {
        label: this.translate.instant('pages.employee_management.contract_status.approved'),
        value: ContractStatus.Approved,
      },
      {
        label: this.translate.instant('pages.employee_management.contract_status.rejected'),
        value: ContractStatus.Rejected,
      },
      {
        label: this.translate.instant('pages.employee_management.contract_status.terminated'),
        value: ContractStatus.Terminated,
      },
    ];

    this.activeStatusOptions = [
      { label: this.translate.instant('global.status.active'), value: true },
      { label: this.translate.instant('global.status.inactive'), value: false },
    ];
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
    this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT, employee.id]);
  }

  getContractStatusClass(employee: AdminEmployeeListItem): string {
    const statusName =
      employee.contractStatus?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `contract-status-badge status-${statusName}`;
  }

  getContractStatusLabel(employee: AdminEmployeeListItem): string {
    if (!employee.contractStatus) return '';
    // contractStatus is a string like "Pending", "Active", "Approved", etc.
    const statusKey = employee.contractStatus.toLowerCase();
    return this.translate.instant(`pages.employee_management.contract_status.${statusKey}`);
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

  onPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.onPageChange(offset, limit);
  }

  // Filter drawer methods
  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  getActiveFilterChips(): { key: string; label: string; value: string }[] {
    const chips: { key: string; label: string; value: string }[] = [];
    const values = this.filterForm.value;

    if (values.searchTerm) {
      chips.push({
        key: 'searchTerm',
        label: this.translate.instant('pages.employee_management.filters.search'),
        value: values.searchTerm,
      });
    }

    if (values.contractStatus && values.contractStatus.length > 0) {
      const statusLabels = values.contractStatus
        .map((s) => this.contractStatusMultiOptions.find((o) => o.value === s)?.label)
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'contractStatus',
        label: this.translate.instant('pages.employee_management.filters.contract_status'),
        value: statusLabels,
      });
    }

    if (values.isActive !== null && values.isActive !== undefined) {
      const activeLabel = this.activeStatusOptions.find((o) => o.value === values.isActive)?.label;
      if (activeLabel) {
        chips.push({
          key: 'isActive',
          label: this.translate.instant('pages.employee_management.filters.active_status'),
          value: activeLabel,
        });
      }
    }

    return chips;
  }

  removeFilterChip(key: string): void {
    if (key === 'contractStatus') {
      this.filterForm.patchValue({ contractStatus: [] });
    } else if (key === 'isActive') {
      this.filterForm.patchValue({ isActive: null });
    } else {
      this.filterForm.patchValue({ [key]: '' });
    }
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  // Checkbox helper methods for contract status
  isContractStatusChecked(status: ContractStatus): boolean {
    return this.filterForm.value.contractStatus?.includes(status) ?? false;
  }

  toggleContractStatus(status: ContractStatus): void {
    const isChecked = this.isContractStatusChecked(status);
    this.onContractStatusChange(status, !isChecked);
  }

  onContractStatusChange(status: ContractStatus, checked: boolean): void {
    const currentStatuses = [...(this.filterForm.value.contractStatus || [])];

    if (checked) {
      if (!currentStatuses.includes(status)) {
        currentStatuses.push(status);
      }
    } else {
      const index = currentStatuses.indexOf(status);
      if (index > -1) {
        currentStatuses.splice(index, 1);
      }
    }

    this.filterForm.patchValue({ contractStatus: currentStatuses });
  }

  // Radio helper method for active status
  onActiveStatusSelect(value: boolean | null): void {
    this.filterForm.patchValue({ isActive: value });
  }
}
