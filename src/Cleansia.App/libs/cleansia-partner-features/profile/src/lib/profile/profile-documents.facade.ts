import { Injectable, computed, inject, signal } from '@angular/core';
import {
  BlobFileDto,
  DocumentType,
  PartnerClient,
  RequestMyDocumentDeletionRequest,
  SaveMyDocumentsCommand,
  SaveMyDocumentsDocumentToSave,
} from '@cleansia/partner-services';
import {
  FileValidationErrorService,
  SnackbarService,
} from '@cleansia/services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { checkEmployeeCurrent } from '@cleansia/partner-stores';
import { FileTransformationUtils } from '@cleansia/utils';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { catchError, of, takeUntil } from 'rxjs';

export interface StagedDocument {
  file: BlobFileDto;
  documentType: DocumentType;
  description?: string;
  preview: string;
}

export interface MyDocument {
  documentId?: string;
  fileName?: string;
  blobUrl?: string;
  documentType?: DocumentType;
  status?: number;
  version?: number;
  fileSizeBytes?: number;
  contentType?: string;
  uploadedAt?: Date;
  description?: string;
  reviewNotes?: string;
}

export interface DocumentsState {
  documents: MyDocument[];
  stagedDocuments: StagedDocument[];
  loading: boolean;
  saving: boolean;
  requestingDeletion: boolean;
}

@Injectable()
export class ProfileDocumentsFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly fileValidationErrorService = inject(
    FileValidationErrorService
  );
  private readonly store = inject(Store);

  // Documents state
  private readonly documentsState = signal<DocumentsState>({
    documents: [],
    stagedDocuments: [],
    loading: false,
    saving: false,
    requestingDeletion: false,
  });

  // Documents selectors
  readonly documents = computed(() => this.documentsState().documents);
  readonly stagedDocuments = computed(
    () => this.documentsState().stagedDocuments
  );
  readonly documentsLoading = computed(() => this.documentsState().loading);
  readonly documentsSaving = computed(() => this.documentsState().saving);
  readonly deletionRequestInFlight = computed(
    () => this.documentsState().requestingDeletion
  );
  readonly hasStagedDocuments = computed(
    () => this.documentsState().stagedDocuments.length > 0
  );

  // Group documents by status
  readonly pendingDocuments = computed(() =>
    this.documents().filter((d) => d.status === 1)
  );
  readonly approvedDocuments = computed(() =>
    this.documents().filter((d) => d.status === 2)
  );
  readonly rejectedDocuments = computed(() =>
    this.documents().filter((d) => d.status === 3)
  );

  async loadEmployeeDocuments(): Promise<void> {
    this.documentsState.update((s) => ({ ...s, loading: true }));

    try {
      const response = await this.partnerClient.employeeClient
        .getMyDocuments()
        .toPromise();

      if (response) {
        this.documentsState.update((s) => ({
          ...s,
          documents: response.documents || [],
          loading: false,
        }));
      }
    } catch {
      this.documentsState.update((s) => ({ ...s, loading: false }));
    }
  }

  async onEmployeeDocumentFilesSelected(
    files: File[],
    documentType: DocumentType
  ): Promise<void> {
    if (!files || files.length === 0) {
      return;
    }

    // Validate files
    const validationResult = FileTransformationUtils.validateFiles(files, {
      maxSizeInMB: 10,
      allowedTypes: ['.pdf', '.doc', '.docx', '.jpg', '.jpeg', '.png'],
    });

    if (!validationResult.isValid) {
      this.fileValidationErrorService.handleFileValidationErrors(
        validationResult.errors
      );
      return;
    }

    // Convert and stage files
    for (const file of files) {
      await this.stageEmployeeDocument(file, documentType);
    }
  }

  private async stageEmployeeDocument(
    file: File,
    documentType: DocumentType
  ): Promise<void> {
    try {
      const reader = new FileReader();

      const base64Promise = new Promise<string>((resolve, reject) => {
        reader.onload = () => resolve(reader.result as string);
        reader.onerror = reject;
        reader.readAsDataURL(file);
      });

      const base64Content = await base64Promise;

      const blobFileDto = new BlobFileDto();
      blobFileDto.fileName = file.name;
      blobFileDto.base64Content = base64Content;
      blobFileDto.contentType = file.type;

      const stagedDoc: StagedDocument = {
        file: blobFileDto,
        documentType: documentType,
        preview: base64Content,
      };

      this.documentsState.update((s) => ({
        ...s,
        stagedDocuments: [...s.stagedDocuments, stagedDoc],
      }));
    } catch (error) {
      console.error('Failed to stage document', error);
      this.snackbarService.showError(
        this.translate.instant('global.messages.documents.stage_error', {
          fileName: file.name,
        })
      );
    }
  }

  removeStagedEmployeeDocument(index: number): void {
    this.documentsState.update((s) => ({
      ...s,
      stagedDocuments: s.stagedDocuments.filter((_, i) => i !== index),
    }));
  }

  updateStagedDocumentType(index: number, documentType: DocumentType): void {
    this.documentsState.update((s) => ({
      ...s,
      stagedDocuments: s.stagedDocuments.map((d, i) =>
        i === index ? { ...d, documentType } : d
      ),
    }));
  }

  async saveEmployeeDocuments(): Promise<void> {
    const staged = this.documentsState().stagedDocuments;

    if (staged.length === 0) {
      this.snackbarService.showError(
        this.translate.instant('global.messages.documents.no_documents_to_save')
      );
      return;
    }

    this.documentsState.update((s) => ({ ...s, saving: true }));

    try {
      const command = new SaveMyDocumentsCommand();
      command.documents = staged.map((d) => {
        const document = new SaveMyDocumentsDocumentToSave();
        document.documentType = d.documentType;
        document.file = d.file;
        document.description = d.description;
        return document;
      });

      await this.partnerClient.employeeClient
        .saveMyDocuments(command)
        .toPromise();

      this.snackbarService.showSuccess(
        this.translate.instant('global.messages.documents.upload_success')
      );

      // Clear staged documents and reload
      this.documentsState.update((s) => ({
        ...s,
        stagedDocuments: [],
        saving: false,
      }));
      await this.loadEmployeeDocuments();
      this.store.dispatch(checkEmployeeCurrent());
    } catch {
      this.documentsState.update((s) => ({ ...s, saving: false }));
    }
  }

  /**
   * Ask an admin to remove a document. It removes NOTHING.
   *
   * This replaced a partner-facing delete. That one soft-deleted on the spot, which flipped
   * `AreDocumentsUploaded` and re-engaged the registration lock — one click cost a cleaner their
   * access to work, on documents the employer is required to hold.
   *
   * The list is deliberately NOT mutated and `checkEmployeeCurrent` is deliberately NOT dispatched:
   * nothing about the cleaner changed, and a screen that looked different afterwards would be lying
   * about what just happened.
   *
   * The reason is collected by the caller and required by the server — without one an admin is being
   * asked to rule on nothing, which is the whole point of routing this past a person. The error path
   * is left to the interceptor, which renders `employee_document.deletion_already_requested` when a
   * request is already open.
   */
  async requestDocumentDeletion(
    documentId: string,
    reason: string
  ): Promise<void> {
    this.documentsState.update((s) => ({ ...s, requestingDeletion: true }));

    const body = new RequestMyDocumentDeletionRequest();
    body.reason = reason;

    try {
      await this.partnerClient.employeeClient
        .requestMyDocumentDeletion(documentId, body)
        .toPromise();

      this.snackbarService.showSuccess(
        this.translate.instant('global.messages.documents.deletion_requested')
      );
    } finally {
      this.documentsState.update((s) => ({ ...s, requestingDeletion: false }));
    }
  }

  downloadEmployeeDocument(documentId: string, fileName: string): void {
    this.partnerClient.employeeClient
      .downloadMyDocument(documentId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response && response.data) {
          // Create a blob from the byte array
          const blob = new Blob([response.data], {
            type:
              response.headers?.['content-type'] || 'application/octet-stream',
          });

          // Create download link and trigger download
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = fileName;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
        }
      });
  }

  formatFileSize(bytes: number): string {
    return FileTransformationUtils.formatFileSize(bytes);
  }

  getDocumentTypeLabel(type: DocumentType): string {
    const labelKey = `global.document_types.${type}`;
    return this.translate.instant(labelKey);
  }

  getStatusLabel(status: number): string {
    const statusKey = `global.document_status.${status}`;
    return this.translate.instant(statusKey);
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 1:
        return 'status-pending';
      case 2:
        return 'status-approved';
      case 3:
        return 'status-rejected';
      default:
        return '';
    }
  }
}
