import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminEmployeeListItem,
  ContractStatus,
  EmployeeFilter,
  GetPagedEmployeesRequest,
  RejectEmployeeRequest,
  SortDefinition,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';
import {
  RejectDialogComponent,
  RejectDialogData,
  RejectDialogResult,
} from '../components';

export interface EmployeeFilterParams {
  contractStatuses?: ContractStatus[];
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class EmployeeManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly employees = signal<AdminEmployeeListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<EmployeeFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly contractStatusOptions = [
    {
      label: this.translate.instant(
        'pages.employee_management.contract_status.pending'
      ),
      value: ContractStatus.Pending,
    },
    {
      label: this.translate.instant(
        'pages.employee_management.contract_status.active'
      ),
      value: ContractStatus.Active,
    },
    {
      label: this.translate.instant(
        'pages.employee_management.contract_status.approved'
      ),
      value: ContractStatus.Approved,
    },
    {
      label: this.translate.instant(
        'pages.employee_management.contract_status.rejected'
      ),
      value: ContractStatus.Rejected,
    },
    {
      label: this.translate.instant(
        'pages.employee_management.contract_status.terminated'
      ),
      value: ContractStatus.Terminated,
    },
  ];

  loadEmployees(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    const employeeFilter = new EmployeeFilter();
    if (
      filterParams?.contractStatuses &&
      filterParams.contractStatuses.length > 0
    ) {
      employeeFilter.contractStatuses = filterParams.contractStatuses;
    }
    if (filterParams?.searchTerm) {
      employeeFilter.searchTerm = filterParams.searchTerm;
    }
    if (filterParams?.isActive !== undefined) {
      employeeFilter.isActive = filterParams.isActive;
    }

    const request = new GetPagedEmployeesRequest({
      offset: this.currentOffset(),
      limit: this.currentLimit(),
      filter: employeeFilter,
      sort: this.currentSort(),
    });

    this.adminClient.adminEmployeeClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.employee_management.messages.load_error'
            )
          );
          console.error('Error loading employees:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.employees.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadEmployees();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadEmployees();
  }

  applyFilter(filter: EmployeeFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadEmployees();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadEmployees();
  }

  approveEmployee(employeeId: string): void {
    this.adminClient.adminEmployeeClient
      .approve(employeeId, undefined)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.employee_management.messages.approve_error'
            )
          );
          console.error('Error approving employee:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_management.messages.approve_success'
            )
          );
          this.loadEmployees();
        }
      });
  }

  rejectEmployee(employeeId: string, reason: string): void {
    const request = new RejectEmployeeRequest({ reason });
    this.adminClient.adminEmployeeClient
      .reject(employeeId, request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.employee_management.messages.reject_error'
            )
          );
          console.error('Error rejecting employee:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_management.messages.reject_success'
            )
          );
          this.loadEmployees();
        }
      });
  }

  openRejectDialog(employee: AdminEmployeeListItem): void {
    if (!employee.id) return;

    const dialogData: RejectDialogData = {
      title: this.translate.instant(
        'pages.employee_management.reject_dialog.title'
      ),
      subtitle: this.translate.instant(
        'pages.employee_management.reject_dialog.subtitle'
      ),
    };

    const dialogRef = this.dialogService.open(RejectDialogComponent, {
      data: dialogData,
      header: this.translate.instant(
        'pages.employee_management.reject_dialog.title'
      ),
      width: '500px',
      modal: true,
    });

    dialogRef.onClose.subscribe((result: RejectDialogResult | undefined) => {
      if (result?.reason) {
        this.rejectEmployee(employee.id!, result.reason);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
