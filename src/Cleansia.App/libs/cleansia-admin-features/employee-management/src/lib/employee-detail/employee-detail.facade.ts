import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminEmployeeDetail,
  AdminUpdateEmployeeAvailabilityRequest,
  AdminUpdateEmployeeCommand,
  BulkCreateEmployeePayConfigsCommand,
  ContractStatus,
  CreatePayConfigCommand,
  EmployeePayConfigDto,
  EmployeePayConfigSummaryDto,
  RejectEmployeeRequest,
  TimeRange,
  UpdatePayConfigCommand,
} from '@cleansia/admin-services';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  RejectDialogComponent,
  RejectDialogData,
  RejectDialogResult,
} from '../components';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

@Injectable()
export class EmployeeDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly dialogService = inject(DialogService);
  private readonly docsFacade = inject(EmployeeDocumentsFacade);

  readonly employee = signal<AdminEmployeeDetail | null>(null);
  readonly loading = signal<boolean>(false);
  readonly editingAvailability = signal<boolean>(false);
  readonly savingAvailability = signal<boolean>(false);
  readonly editingSection = signal<string | null>(null);
  readonly savingEmployee = signal<boolean>(false);
  readonly countries = signal<ICleansiaSelectOption[]>([]);

  // Pay config
  readonly employeePayConfigs = signal<EmployeePayConfigDto[]>([]);
  readonly payConfigSummary = signal<EmployeePayConfigSummaryDto | null>(null);
  readonly loadingPayConfigs = signal<boolean>(false);
  readonly savingPayConfig = signal<boolean>(false);
  readonly bulkApplyingGrade = signal<boolean>(false);
  readonly services = signal<ICleansiaSelectOption[]>([]);
  readonly packages = signal<ICleansiaSelectOption[]>([]);
  readonly currencies = signal<ICleansiaSelectOption[]>([]);

  loadEmployeeDetail(employeeId: string): void {
    this.loading.set(true);

    this.adminClient.adminEmployeeClient
      .details(employeeId)
      .pipe(
        takeUntil(this.destroyed$),
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
      .pipe(takeUntil(this.destroyed$), catchError(() => of([])))
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
        takeUntil(this.destroyed$),
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
        takeUntil(this.destroyed$),
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
        takeUntil(this.destroyed$),
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
    const employee = this.employee();
    const employeeId = employee?.id;
    if (!employee || !employeeId) return;

    this.savingEmployee.set(true);

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
        takeUntil(this.destroyed$),
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

  // Pay config methods
  loadEmployeePayConfigs(employeeId: string): void {
    this.loadPayConfigSummary(employeeId);
  }

  loadPayConfigSummary(employeeId: string): void {
    this.loadingPayConfigs.set(true);
    this.adminClient.adminPayConfigClient
      .employeeSummary(employeeId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loadingPayConfigs.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payConfigSummary.set(response);
        }
      });
  }

  bulkApplyGrade(grade: string, currencyId: string, overwriteExisting: boolean): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.bulkApplyingGrade.set(true);

    const command = new BulkCreateEmployeePayConfigsCommand({
      employeeId,
      grade,
      currencyId,
      overwriteExisting,
    });

    this.adminClient.adminPayConfigClient
      .bulkCreateForEmployee(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.employee_detail.messages.pay_config_save_error')
          );
          return of(null);
        }),
        finalize(() => this.bulkApplyingGrade.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.employee_detail.messages.pay_config_bulk_success', {
              created: response.createdCount,
              skipped: response.skippedCount,
            })
          );
          this.loadPayConfigSummary(employeeId);
        }
      });
  }

  updateSinglePayConfig(
    payConfigId: string,
    data: {
      basePay: number;
      extraPerRoom: number;
      extraPerBathroom: number;
      distanceRatePerKm: number;
      minimumPay: number;
      maximumPay: number;
    }
  ): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.savingPayConfig.set(true);

    const command = new UpdatePayConfigCommand({
      payConfigId,
      basePay: data.basePay,
      extraPerRoom: data.extraPerRoom,
      extraPerBathroom: data.extraPerBathroom,
      distanceRatePerKm: data.distanceRatePerKm,
      minimumPay: data.minimumPay,
      maximumPay: data.maximumPay,
      description: undefined,
    });

    this.adminClient.adminPayConfigClient
      .update(payConfigId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.employee_detail.messages.pay_config_save_error')
          );
          return of(null);
        }),
        finalize(() => this.savingPayConfig.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.employee_detail.messages.pay_config_save_success')
          );
          this.loadPayConfigSummary(employeeId);
        }
      });
  }

  loadPayConfigOptions(): void {
    if (this.services().length > 0) return;

    this.adminClient.adminServiceClient
      .getPaged(undefined, undefined, 0, 100)
      .pipe(takeUntil(this.destroyed$), catchError(() => of(null)))
      .subscribe((result) => {
        this.services.set(
          (result?.data ?? []).map((s) => ({ label: s.name ?? '', value: s.id! }))
        );
      });

    this.adminClient.adminPackageClient
      .getPaged(undefined, undefined, 0, 100)
      .pipe(takeUntil(this.destroyed$), catchError(() => of(null)))
      .subscribe((result) => {
        this.packages.set(
          (result?.data ?? []).map((p) => ({ label: p.name ?? '', value: p.id! }))
        );
      });

    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(takeUntil(this.destroyed$), catchError(() => of([])))
      .subscribe((currencies) => {
        this.currencies.set(
          (currencies ?? []).map((c) => ({ label: c.code ?? '', value: c.id! }))
        );
      });
  }

  createEmployeePayConfig(data: Record<string, any>): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.savingPayConfig.set(true);

    const command = new CreatePayConfigCommand({
      employeeId,
      serviceId: data['serviceId'] || undefined,
      packageId: data['packageId'] || undefined,
      basePay: data['basePay'] ?? 0,
      extraPerRoom: data['extraPerRoom'] ?? 0,
      extraPerBathroom: data['extraPerBathroom'] ?? 0,
      distanceRatePerKm: data['distanceRatePerKm'] ?? 0,
      minimumPay: data['minimumPay'] ?? 0,
      maximumPay: data['maximumPay'] ?? 0,
      currencyId: data['currencyId'],
      description: data['description'] || undefined,
    });

    this.adminClient.adminPayConfigClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.employee_detail.messages.pay_config_save_error')
          );
          return of(null);
        }),
        finalize(() => this.savingPayConfig.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.employee_detail.messages.pay_config_save_success')
          );
          this.editingSection.set(null);
          this.loadEmployeePayConfigs(employeeId);
        }
      });
  }

  deleteEmployeePayConfig(payConfigId: string): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.adminClient.adminPayConfigClient
      .delete(payConfigId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.employee_detail.messages.pay_config_delete_error')
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.employee_detail.messages.pay_config_delete_success')
          );
          this.loadEmployeePayConfigs(employeeId);
        }
      });
  }

  applyGradeTemplate(multiplier: number): void {
    // Load global configs, apply multiplier, and use as template for the form
    // This will be handled by the component
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
}
