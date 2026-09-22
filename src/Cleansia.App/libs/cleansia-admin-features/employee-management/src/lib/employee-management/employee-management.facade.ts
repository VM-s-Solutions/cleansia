import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminClient,
  AdminEmployeeListItem,
  ApproveEmployeeRequest,
  ContractStatus,
  RejectEmployeeRequest,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, ICleansiaSelectOption, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  ApproveDialogComponent,
  ApproveDialogData,
  ApproveDialogResult,
  RejectDialogComponent,
  RejectDialogData,
  RejectDialogResult,
} from '../components';
import {
  buildActiveStatusOptions,
  buildContractStatusOptions,
  buildFilterChips,
  toggleContractStatusInList,
} from './employee-management.helpers';

export interface EmployeeFilterParams {
  contractStatuses?: ContractStatus[];
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class EmployeeManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
  private readonly translate = inject(TranslateService);

  readonly employees = signal<AdminEmployeeListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly countries = signal<ICleansiaSelectOption[]>([]);

  readonly lang = currentLanguage(this.translate);
  readonly contractStatusOptions = computed(() => {
    this.lang();
    return buildContractStatusOptions(this.translate);
  });
  readonly activeStatusOptions = computed(() => {
    this.lang();
    return buildActiveStatusOptions(this.translate);
  });
  readonly filterForm = inject(FormBuilder).group({
    contractStatus: [[] as ContractStatus[]],
    searchTerm: [''],
    isActive: [null as boolean | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] =>
      buildFilterChips(value, this.contractStatusOptions(), this.activeStatusOptions(), this.translate),
    apply: (value) =>
      this.applyFilter({
        contractStatuses: value.contractStatus?.length ? value.contractStatus : undefined,
        searchTerm: value.searchTerm?.trim() || undefined,
        isActive: value.isActive ?? undefined,
      }),
  });

  private currentFilter = signal<EmployeeFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadEmployees(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminEmployeeClient
      .getPaged(
        undefined, // id
        filterParams?.isActive,
        filterParams?.contractStatuses,
        filterParams?.searchTerm,
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.employees.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        // After first load, set initialLoading to false
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadEmployees();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadEmployees();
  }

  applyFilter(filter: EmployeeFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadEmployees();
  }

  isContractStatusChecked(status: ContractStatus): boolean {
    return this.filterForm.value.contractStatus?.includes(status) ?? false;
  }

  setContractStatus(status: ContractStatus, checked: boolean): void {
    this.filterForm.patchValue({
      contractStatus: toggleContractStatusInList(this.filterForm.value.contractStatus || [], status, checked),
    });
  }

  selectActiveStatus(value: boolean | null): void {
    this.filterForm.patchValue({ isActive: value });
  }

  approveEmployee(employeeId: string, workCountryId: string, notes?: string): void {
    const request = new ApproveEmployeeRequest();
    request.workCountryId = workCountryId;
    request.notes = notes;
    this.adminClient.adminEmployeeClient
      .approve(employeeId, request)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.employee_management.messages.approve_success'
          );
          this.loadEmployees();
        }
      });
  }

  openApproveDialog(employee: AdminEmployeeListItem): void {
    const employeeId = employee.id;
    if (!employeeId) return;

    // Lazy-load countries the first time someone opens the dialog from
    // the list page; thereafter cached on the signal.
    if (this.countries().length === 0) {
      this.loadCountries(() => this.showApproveDialog(employeeId));
      return;
    }
    this.showApproveDialog(employeeId);
  }

  private showApproveDialog(employeeId: string): void {
    const dialogData: ApproveDialogData = {
      subtitle: this.translate.instant(
        'pages.employee_management.approve_dialog.subtitle'
      ),
      countries: this.countries(),
    };

    const dialogRef = this.dialogService.open(ApproveDialogComponent, {
      data: dialogData,
      header: this.translate.instant(
        'pages.employee_management.approve_dialog.title'
      ),
      modal: true,
      closable: true,
      draggable: false,
      resizable: false,
      styleClass: 'cleansia-dialog dialog-panel',
    });

    dialogRef?.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: ApproveDialogResult | undefined) => {
      if (result?.workCountryId) {
        this.approveEmployee(employeeId, result.workCountryId, result.notes);
      }
    });
  }

  private loadCountries(onDone?: () => void): void {
    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(takeUntil(this.destroyed$), catchError(() => of([])))
      .subscribe((countries) => {
        const currentLang = this.translate.currentLang;
        const options: ICleansiaSelectOption[] = (countries ?? []).map((country) => {
          const translation = country.translations?.[currentLang]?.name;
          const name = translation ?? country.name ?? '';
          const iso = country.isoCode ?? '';
          return {
            label: iso ? `${name} (${iso})` : name,
            value: country.id,
          };
        });
        this.countries.set(options);
        onDone?.();
      });
  }

  rejectEmployee(employeeId: string, reason: string): void {
    const request = new RejectEmployeeRequest();
    request.reason = reason;
    this.adminClient.adminEmployeeClient
      .reject(employeeId, request)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.employee_management.messages.reject_success'
          );
          this.loadEmployees();
        }
      });
  }

  openRejectDialog(employee: AdminEmployeeListItem): void {
    const employeeId = employee.id;
    if (!employeeId) return;

    const dialogData: RejectDialogData = {
      subtitle: this.translate.instant(
        'pages.employee_management.reject_dialog.subtitle'
      ),
    };

    const dialogRef = this.dialogService.open(RejectDialogComponent, {
      data: dialogData,
      header: this.translate.instant(
        'pages.employee_management.reject_dialog.title'
      ),
      modal: true,
      closable: true,
      draggable: false,
      resizable: false,
      styleClass: 'cleansia-dialog dialog-panel',
    });

    dialogRef?.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: RejectDialogResult | undefined) => {
      if (result?.reason) {
        this.rejectEmployee(employeeId, result.reason);
      }
    });
  }
}
