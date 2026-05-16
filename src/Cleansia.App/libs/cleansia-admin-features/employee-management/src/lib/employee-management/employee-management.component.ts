import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
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
import {
  buildActiveStatusOptions,
  buildContractStatusOptions,
  buildFilterChips,
  getContractStatusClass,
  getContractStatusLabel,
  toggleContractStatusInList,
} from './employee-management.helpers';
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
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './employee-management.component.html',
  providers: [EmployeeManagementFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
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

  readonly ContractStatus = ContractStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    contractStatus: [[] as ContractStatus[]],
    searchTerm: [''],
    isActive: [null as boolean | null],
  });

  contractStatusMultiOptions: { label: string; value: ContractStatus }[] = [];
  activeStatusOptions: Array<{ label: string; value: boolean }> = [];

  isFilterDrawerOpen = signal(false);
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return buildFilterChips(
      this.filterForm.value,
      this.contractStatusMultiOptions,
      this.activeStatusOptions,
      this.translate
    );
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update((v) => v + 1);
      });

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      });

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
    this.contractStatusMultiOptions = buildContractStatusOptions(this.translate);
    this.activeStatusOptions = buildActiveStatusOptions(this.translate);
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
    return getContractStatusClass(employee);
  }

  getContractStatusLabel(employee: AdminEmployeeListItem): string {
    return getContractStatusLabel(employee, this.translate);
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
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    this.facade.onSortChange([
      new SortDefinition({ field: event.field, direction: sortDirection }),
    ]);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
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

  isContractStatusChecked(status: ContractStatus): boolean {
    return this.filterForm.value.contractStatus?.includes(status) ?? false;
  }

  toggleContractStatus(status: ContractStatus): void {
    const isChecked = this.isContractStatusChecked(status);
    this.onContractStatusChange(status, !isChecked);
  }

  onContractStatusChange(status: ContractStatus, checked: boolean): void {
    const updated = toggleContractStatusInList(
      this.filterForm.value.contractStatus || [],
      status,
      checked
    );
    this.filterForm.patchValue({ contractStatus: updated });
  }

  onActiveStatusSelect(value: boolean | null): void {
    this.filterForm.patchValue({ isActive: value });
  }
}
