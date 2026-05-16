import { Injectable, computed, inject, signal } from '@angular/core';
import {
  BlobFileDto,
  DocumentType,
  PartnerClient,
  SaveMyDocumentsCommand,
  SaveMyDocumentsDocumentToSave,
} from '@cleansia/partner-services';
import {
  DialogService,
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
  deleting: boolean;
}

@Injectable()
export class ProfileDocumentsFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
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
    deleting: false,
  });

  // Documents selectors
  readonly documents = computed(() => this.documentsState().documents);
  readonly stagedDocuments = computed(
    () => this.documentsState().stagedDocuments
  );
  readonly documentsLoading = computed(() => this.documentsState().loading);
  readonly documentsSaving = computed(() => this.documentsState().saving);
  readonly documentsDeleting = computed(() => this.documentsState().deleting);
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

      const blobFileDto = new BlobFileDto({
        fileName: file.name,
        base64Content: base64Content,
        contentType: file.type,
      });

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
      const documentsToSave = staged.map(
        (d) =>
          new SaveMyDocumentsDocumentToSave({
            documentType: d.documentType,
            file: d.file,
            description: d.description,
          })
      );

      const command = new SaveMyDocumentsCommand({
        documents: documentsToSave,
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

  async deleteEmployeeDocument(
    documentId: string,
    fileName: string
  ): Promise<void> {
    const confirmed = await this.dialogService
      .confirm(
        this.translate.instant('global.messages.documents.delete_confirm', {
          fileName,
        })
      )
      .toPromise();

    if (!confirmed) {
      return;
    }

    this.documentsState.update((s) => ({ ...s, deleting: true }));

    try {
      await this.partnerClient.employeeClient
        .deleteMyDocument(documentId)
        .toPromise();

      this.snackbarService.showSuccess(
        this.translate.instant('global.messages.documents.delete_success')
      );

      // Remove from state
      this.documentsState.update((s) => ({
        ...s,
        documents: s.documents.filter((d) => d.documentId !== documentId),
        deleting: false,
      }));
      this.store.dispatch(checkEmployeeCurrent());
    } catch {
      this.documentsState.update((s) => ({ ...s, deleting: false }));
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
