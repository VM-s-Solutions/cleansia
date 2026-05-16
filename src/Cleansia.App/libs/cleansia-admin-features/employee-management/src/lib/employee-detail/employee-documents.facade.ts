import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  DocumentStatus,
  DocumentType,
  EmployeeDocumentFilter,
  EmployeeDocumentItem,
  GetEmployeeDocumentsRequest,
  RejectDocumentCommand,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
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

@Injectable()
export class EmployeeDocumentsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly dialogService = inject(DialogService);

  readonly documents = signal<EmployeeDocumentItem[]>([]);
  readonly documentsLoading = signal<boolean>(false);

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
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.documentsLoading.set(false))
      )
      .subscribe((response) => {
        if (response?.data) {
          this.documents.set(response.data);
        }
      });
  }

  approveDocument(documentId: string, employeeId: string | undefined): void {
    this.adminClient.adminEmployeeDocumentClient
      .approve(documentId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.document_approve_success'
            )
          );
          if (employeeId) {
            this.loadEmployeeDocuments(employeeId);
          }
        }
      });
  }

  rejectDocument(documentId: string, reason: string, employeeId: string | undefined): void {
    const command = new RejectDocumentCommand({
      documentId,
      notes: reason,
    });

    this.adminClient.adminEmployeeDocumentClient
      .reject(documentId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.employee_detail.messages.document_reject_success'
            )
          );
          if (employeeId) {
            this.loadEmployeeDocuments(employeeId);
          }
        }
      });
  }

  openRejectDocumentDialog(employeeDocument: EmployeeDocumentItem, employeeId: string | undefined): void {
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
        this.rejectDocument(employeeDocument.id, result.reason, employeeId);
      }
    });
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
        takeUntil(this.destroyed$),
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
}
