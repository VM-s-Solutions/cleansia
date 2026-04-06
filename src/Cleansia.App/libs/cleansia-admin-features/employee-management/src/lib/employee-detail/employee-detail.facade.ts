import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminUpdateEmployeeAvailabilityRequest,
  AdminUpdateEmployeeCommand,
  ContractStatus,
  RejectEmployeeRequest,
  TimeRange,
} from '@cleansia/admin-services';
import { ICleansiaSelectOption } from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';
import {
  RejectDialogComponent,
  RejectDialogData,
  RejectDialogResult,
} from '../components';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

@Injectable()
export class EmployeeDetailFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly dialogService = inject(DialogService);
  private readonly docsFacade = inject(EmployeeDocumentsFacade);

  private destroy$ = new Subject<void>();

  readonly employee = signal<any>(null);
  readonly loading = signal<boolean>(false);
  readonly editingAvailability = signal<boolean>(false);
  readonly savingAvailability = signal<boolean>(false);
  readonly editingSection = signal<string | null>(null);
  readonly savingEmployee = signal<boolean>(false);
  readonly countries = signal<ICleansiaSelectOption[]>([]);

  loadEmployeeDetail(employeeId: string): void {
    this.loading.set(true);

    this.adminClient.adminEmployeeClient
      .details(employeeId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.employee.set(response);
          this.docsFacade.loadEmployeeDocuments(employeeId);
          this.loadCountries();
        }
      });
  }

  private loadCountries(): void {
    if (this.countries().length > 0) {
      return;
    }
    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(takeUntil(this.destroy$), catchError(() => of([])))
      .subscribe((countries) => {
        const currentLang = this.translate.currentLang;
        const options: ICleansiaSelectOption[] = (countries ?? []).map((country) => {
          const translation = country.translations?.[currentLang]?.name;
          const name = translation ?? country.name ?? '';
          const iso = country.isoCode ?? '';
          return {
            label: iso ? `${name} (${iso})` : name,
            value: country.id!,
          };
        });
        this.countries.set(options);
      });
  }

  approveEmployee(): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.adminClient.adminEmployeeClient
      .approve(employeeId, undefined)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.employee_approve_success'
            )
          );
          this.loadEmployeeDetail(employeeId);
        }
      });
  }

  rejectEmployee(reason: string): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    const request = new RejectEmployeeRequest({ reason });
    this.adminClient.adminEmployeeClient
      .reject(employeeId, request)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.employee_reject_success'
            )
          );
          this.loadEmployeeDetail(employeeId);
        }
      });
  }

  openRejectEmployeeDialog(): void {
    const employee = this.employee();
    if (!employee?.id) return;

    const dialogData: RejectDialogData = {
      title: this.translate.instant(
        'pages.employee_detail.reject_employee_dialog.title'
      ),
      subtitle: this.translate.instant(
        'pages.employee_detail.reject_employee_dialog.subtitle'
      ),
    };

    const dialogRef = this.dialogService.open(RejectDialogComponent, {
      data: dialogData,
      header: this.translate.instant(
        'pages.employee_detail.reject_employee_dialog.title'
      ),
      width: '500px',
      modal: true,
    });

    dialogRef.onClose.subscribe((result: RejectDialogResult | undefined) => {
      if (result?.reason) {
        this.rejectEmployee(result.reason);
      }
    });
  }

  startEditingAvailability(): void {
    this.editingAvailability.set(true);
  }

  cancelEditingAvailability(): void {
    this.editingAvailability.set(false);
  }

  saveAvailability(availability: { [key: string]: TimeRange[] } | undefined): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.savingAvailability.set(true);

    const request = new AdminUpdateEmployeeAvailabilityRequest({ availability });

    this.adminClient.adminEmployeeClient
      .updateAvailability(employeeId, request)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.employee_detail.messages.availability_save_error'
            )
          );
          return of(null);
        }),
        finalize(() => this.savingAvailability.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.availability_save_success'
            )
          );
          this.editingAvailability.set(false);
          this.loadEmployeeDetail(employeeId);
        }
      });
  }

  startEditingSection(section: string): void {
    this.editingSection.set(section);
  }

  cancelEditingSection(): void {
    this.editingSection.set(null);
  }

  updateEmployee(data: Record<string, any>): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.savingEmployee.set(true);

    const employee = this.employee();
    const command = new AdminUpdateEmployeeCommand({
      employeeId,
      firstName: data['firstName'] ?? employee.firstName,
      lastName: data['lastName'] ?? employee.lastName,
      phone: data['phoneNumber'] ?? data['phone'] ?? employee.phoneNumber,
      birthDate: data['birthDate'] ?? employee.birthDate,
      street: data['street'] ?? employee.street,
      city: data['city'] ?? employee.city,
      zipCode: data['zipCode'] ?? employee.zipCode,
      countryId: data['countryId'] ?? employee.countryId,
      state: data['state'] ?? employee.state,
      nationalityId: data['nationalityId'] ?? employee.nationalityId,
      passportId: data['passportId'] ?? employee.passportId,
      entityType: data['entityType'] ?? employee.entityType,
      registrationNumber: data['registrationNumber'] ?? employee.registrationNumber,
      vatNumber: data['vatNumber'] ?? employee.vatNumber,
      legalEntityName: data['legalEntityName'] ?? employee.legalEntityName,
      iban: data['iban'] ?? employee.iban,
      emergencyName:
        data['emergencyContactName'] ?? data['emergencyName'] ?? employee.emergencyContactName,
      emergencyPhone:
        data['emergencyContactPhone'] ?? data['emergencyPhone'] ?? employee.emergencyContactPhone,
    });

    this.adminClient.adminEmployeeClient
      .update(employeeId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.employee_detail.messages.employee_update_error'
            )
          );
          return of(null);
        }),
        finalize(() => this.savingEmployee.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.employee_update_success'
            )
          );
          this.editingSection.set(null);
          this.loadEmployeeDetail(employeeId);
        }
      });
  }

  canApproveOrReject(): boolean {
    const employee = this.employee();
    return (
      employee?.isProfileComplete === true &&
      employee?.contractStatus === ContractStatus[ContractStatus.Pending]
    );
  }

  // Format date for display
  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('en-GB');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
