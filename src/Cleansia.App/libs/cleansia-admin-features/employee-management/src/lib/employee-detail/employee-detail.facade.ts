import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminUpdateEmployeeAvailabilityRequest,
  AdminUpdateEmployeeCommand,
  ContractStatus,
  DocumentStatus,
  DocumentType,
  EmployeeDocumentFilter,
  EmployeeDocumentItem,
  GetEmployeeDocumentsRequest,
  RejectDocumentCommand,
  RejectEmployeeRequest,
  SortDefinition,
  SortDirection,
  TimeRange,
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

@Injectable()
export class EmployeeDetailFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly dialogService = inject(DialogService);

  private destroy$ = new Subject<void>();

  readonly employee = signal<any>(null);
  readonly loading = signal<boolean>(false);
  readonly documents = signal<EmployeeDocumentItem[]>([]);
  readonly documentsLoading = signal<boolean>(false);
  readonly editingAvailability = signal<boolean>(false);
  readonly savingAvailability = signal<boolean>(false);
  readonly editingSection = signal<string | null>(null);
  readonly savingEmployee = signal<boolean>(false);

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
          // Load documents for this employee
          this.loadEmployeeDocuments(employeeId);
        }
      });
  }

  loadEmployeeDocuments(employeeId: string): void {
    this.documentsLoading.set(true);

    const filter = new EmployeeDocumentFilter();
    filter.employeeId = employeeId;
    filter.latestVersionOnly = true;
    filter.isActive = true;

    const sort = [
      new SortDefinition({
        field: 'CreatedOn',
        direction: SortDirection.Descending,
      }),
    ];

    const request = new GetEmployeeDocumentsRequest({
      filter,
      sort,
      offset: 0,
      limit: 100,
    });

    this.adminClient.adminEmployeeDocumentClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.documentsLoading.set(false))
      )
      .subscribe((response) => {
        if (response?.data) {
          this.documents.set(response.data);
        }
      });
  }

  approveDocument(documentId: string): void {
    this.adminClient.adminEmployeeDocumentClient
      .approve(documentId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.document_approve_success'
            )
          );
          // Reload documents to reflect the change
          const employeeId = this.employee()?.id;
          if (employeeId) {
            this.loadEmployeeDocuments(employeeId);
          }
        }
      });
  }

  rejectDocument(documentId: string, reason: string): void {
    const command = new RejectDocumentCommand({
      documentId,
      notes: reason,
    });

    this.adminClient.adminEmployeeDocumentClient
      .reject(documentId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.document_reject_success'
            )
          );
          // Reload documents to reflect the change
          const employeeId = this.employee()?.id;
          if (employeeId) {
            this.loadEmployeeDocuments(employeeId);
          }
        }
      });
  }

  openRejectDocumentDialog(employeeDocument: EmployeeDocumentItem): void {
    if (!employeeDocument.id) return;

    const dialogData: RejectDialogData = {
      title: this.translate.instant(
        'pages.employee_detail.reject_document_dialog.title'
      ),
      subtitle: this.translate.instant(
        'pages.employee_detail.reject_document_dialog.subtitle'
      ),
    };

    const dialogRef = this.dialogService.open(RejectDialogComponent, {
      data: dialogData,
      header: this.translate.instant(
        'pages.employee_detail.reject_document_dialog.title'
      ),
      width: '500px',
      modal: true,
    });

    dialogRef.onClose.subscribe((result: RejectDialogResult | undefined) => {
      if (result?.reason && employeeDocument.id) {
        this.rejectDocument(employeeDocument.id, result.reason);
      }
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

  updateEmployee(data: Partial<AdminUpdateEmployeeCommand>): void {
    const employeeId = this.employee()?.id;
    if (!employeeId) return;

    this.savingEmployee.set(true);

    const employee = this.employee();
    const command = new AdminUpdateEmployeeCommand({
      firstName: data.firstName ?? employee.firstName,
      lastName: data.lastName ?? employee.lastName,
      phoneNumber: data.phoneNumber ?? employee.phoneNumber,
      birthDate: data.birthDate ?? employee.birthDate,
      street: data.street ?? employee.street,
      city: data.city ?? employee.city,
      zipCode: data.zipCode ?? employee.zipCode,
      countryId: data.countryId ?? employee.countryId,
      nationalityId: data.nationalityId ?? employee.nationalityId,
      passportId: data.passportId ?? employee.passportId,
      taxId: data.taxId ?? employee.taxId,
      iban: data.iban ?? employee.iban,
      emergencyContactName: data.emergencyContactName ?? employee.emergencyContactName,
      emergencyContactPhone: data.emergencyContactPhone ?? employee.emergencyContactPhone,
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

  downloadDocument(employeeDocument: EmployeeDocumentItem): void {
    if (!employeeDocument.id) {
      this.snackbarService.showError(
        this.translate.instant(
          'pages.employee_detail.messages.document_download_error'
        )
      );
      return;
    }

    this.downloadDocumentBlob(employeeDocument.id).subscribe((fileResponse) => {
      if (fileResponse && fileResponse.data) {
        this.triggerDownload(
          fileResponse.data,
          fileResponse.fileName || employeeDocument.fileName || 'document'
        );
      }
    });
  }

  previewDocument(employeeDocument: EmployeeDocumentItem): void {
    if (!employeeDocument.id) {
      this.snackbarService.showError(
        this.translate.instant(
          'pages.employee_detail.messages.document_download_error'
        )
      );
      return;
    }

    this.downloadDocumentBlob(employeeDocument.id).subscribe((fileResponse) => {
      if (fileResponse && fileResponse.data) {
        this.openBlobInNewTab(fileResponse.data);
      }
    });
  }

  private downloadDocumentBlob(documentId: string) {
    return this.adminClient.adminEmployeeDocumentClient
      .download(documentId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      );
  }

  private triggerDownload(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  private openBlobInNewTab(blob: Blob): void {
    const url = window.URL.createObjectURL(blob);
    window.open(url, '_blank');
    setTimeout(() => window.URL.revokeObjectURL(url), 1000);
  }

  // Group documents by status for display
  get pendingDocuments(): EmployeeDocumentItem[] {
    return this.documents().filter(
      (doc) => doc.status === DocumentStatus.Pending
    );
  }

  get approvedDocuments(): EmployeeDocumentItem[] {
    return this.documents().filter(
      (doc) => doc.status === DocumentStatus.Approved
    );
  }

  get rejectedDocuments(): EmployeeDocumentItem[] {
    return this.documents().filter(
      (doc) => doc.status === DocumentStatus.Rejected
    );
  }

  // Format file size for display
  formatFileSize(sizeInBytes: number | null | undefined): string {
    if (!sizeInBytes) return '-';

    const kb = sizeInBytes / 1024;
    if (kb < 1024) {
      return `${kb.toFixed(1)} KB`;
    }

    const mb = kb / 1024;
    return `${mb.toFixed(1)} MB`;
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

  // Get status badge class
  getDocumentStatusClass(status: DocumentStatus | null | undefined): string {
    if (!status) return 'status-badge status-unknown';

    switch (status) {
      case DocumentStatus.Pending:
        return 'status-badge status-pending';
      case DocumentStatus.Approved:
        return 'status-badge status-approved';
      case DocumentStatus.Rejected:
        return 'status-badge status-rejected';
      default:
        return 'status-badge status-unknown';
    }
  }

  // Get human-readable document status label
  getDocumentStatusLabel(status: DocumentStatus | null | undefined): string {
    if (!status) {
      return this.translate.instant(
        'pages.employee_detail.document_status.unknown'
      );
    }

    switch (status) {
      case DocumentStatus.Pending:
        return this.translate.instant(
          'pages.employee_detail.document_status.pending'
        );
      case DocumentStatus.Approved:
        return this.translate.instant(
          'pages.employee_detail.document_status.approved'
        );
      case DocumentStatus.Rejected:
        return this.translate.instant(
          'pages.employee_detail.document_status.rejected'
        );
      default:
        return this.translate.instant(
          'pages.employee_detail.document_status.unknown'
        );
    }
  }

  // Get human-readable document type label
  getDocumentTypeLabel(type: DocumentType | null | undefined): string {
    if (!type) {
      return this.translate.instant(
        'pages.employee_detail.document_types.unknown'
      );
    }

    switch (type) {
      case DocumentType.IdentityCard:
        return this.translate.instant(
          'pages.employee_detail.document_types.identity_card'
        );
      case DocumentType.Passport:
        return this.translate.instant(
          'pages.employee_detail.document_types.passport'
        );
      case DocumentType.DriversLicense:
        return this.translate.instant(
          'pages.employee_detail.document_types.drivers_license'
        );
      case DocumentType.WorkPermit:
        return this.translate.instant(
          'pages.employee_detail.document_types.work_permit'
        );
      case DocumentType.Contract:
        return this.translate.instant(
          'pages.employee_detail.document_types.contract'
        );
      case DocumentType.Certificate:
        return this.translate.instant(
          'pages.employee_detail.document_types.certificate'
        );
      case DocumentType.BankStatement:
        return this.translate.instant(
          'pages.employee_detail.document_types.bank_statement'
        );
      case DocumentType.TaxDocument:
        return this.translate.instant(
          'pages.employee_detail.document_types.tax_document'
        );
      case DocumentType.InsuranceDocument:
        return this.translate.instant(
          'pages.employee_detail.document_types.insurance_document'
        );
      case DocumentType.Other:
        return this.translate.instant(
          'pages.employee_detail.document_types.other'
        );
      default:
        return this.translate.instant(
          'pages.employee_detail.document_types.unknown'
        );
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
