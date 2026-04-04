import { Injectable, computed, inject, signal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
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
import { FileTransformationUtils, FormUtils } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import {
  Observable,
  catchError,
  combineLatest,
  finalize,
  from,
  of,
  shareReplay,
  switchMap,
  takeUntil,
  tap,
} from 'rxjs';
import { ProfileFormFactory } from './profile.models';

interface StagedDocument {
  file: BlobFileDto;
  documentType: DocumentType;
  description?: string;
  preview: string;
}

interface MyDocument {
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

interface DocumentsState {
  documents: MyDocument[];
  stagedDocuments: StagedDocument[];
  loading: boolean;
  saving: boolean;
  deleting: boolean;
}

@Injectable()
export class ProfileFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
  private readonly fileValidationErrorService = inject(
    FileValidationErrorService
  );

  readonly formGroup: FormGroup =
    ProfileFormFactory.createEmployeeProfileForm();

  profileLoading = signal(false);
  profileSubmitLoading = signal(false);
  countries = signal<ICleansiaSelectOption[]>([]);

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

  private profileData$: Observable<any> | null = null;

  loadProfile(): void {
    if (this.profileData$) {
      return;
    }

    this.profileLoading.set(true);

    const employee$ = this.partnerClient.employeeClient.getCurrentEmployee();
    const countries$ = this.partnerClient.countryClient.getOverview();

    this.profileData$ = combineLatest([employee$, countries$]).pipe(
      tap(([employee, countries]) => {
        const formData = ProfileFormFactory.mapEmployeeToFormData(employee);
        FormUtils.safePatchValue(this.formGroup, formData);

        const countryOptions: ICleansiaSelectOption[] = countries.map(
          (country) => {
            const translation =
              country.translations?.[this.translate.currentLang]?.name;
            const name = translation ?? country.name!;
            const iso = country.isoCode ?? '';
            return {
              label: iso ? `${name} (${iso})` : name,
              value: country.id!,
            };
          }
        );

        this.countries.set(countryOptions);
        this.profileLoading.set(false);
      }),
      catchError(() => {
        this.profileLoading.set(false);
        this.profileData$ = null;
        return of(null);
      }),
      shareReplay(1),
      takeUntil(this.destroyed$)
    );

    this.profileData$.subscribe();
  }

  refreshProfile(): void {
    this.profileData$ = null;
    this.loadProfile();
  }

  onDocumentUpload(files: File[]): void {
    const normalizedFiles = FileTransformationUtils.normalizeFiles(files);

    if (!normalizedFiles.length) {
      this.snackbarService.showError(
        this.translate.instant('global.messages.profile.no_files_selected')
      );
      return;
    }

    // Update the form control with the selected files
    const documentsControl = this.formGroup.get('documents');
    documentsControl?.setValue(normalizedFiles);
    documentsControl?.markAsTouched();

    // Validate the files
    if (documentsControl?.invalid) {
      this.fileValidationErrorService.handleFileValidationErrors(
        documentsControl.errors
      );
      return;
    }

    this.snackbarService.showSuccess(
      this.translate.instant('global.messages.profile.documents_uploaded')
    );
  }

  removeFile(fileIndex: number): void {
    const currentFiles = ProfileFormFactory.getUploadedFiles(this.formGroup);
    const updatedFiles = FileTransformationUtils.removeFileByIndex(
      currentFiles,
      fileIndex
    );

    if (updatedFiles.length !== currentFiles.length) {
      const newFileList = updatedFiles.length > 0 ? updatedFiles : null;
      const documentsControl = this.formGroup.get('documents');
      documentsControl?.setValue(newFileList);
      documentsControl?.markAsTouched();

      this.snackbarService.showSuccess(
        this.translate.instant('global.messages.profile.file_removed')
      );
    }
  }

  onSubmit(): void {
    if (!this.formGroup.valid) {
      this.snackbarService.showError(
        this.translate.instant('global.messages.profile.fill_required_fields')
      );
      FormUtils.markAllFieldsAsTouched(this.formGroup);
      return;
    }

    this.profileSubmitLoading.set(true);

    const documents = ProfileFormFactory.getUploadedFiles(this.formGroup);

    from(
      FileTransformationUtils.convertFilesToBlobFileDtos(documents, {
        maxSizeInMB: 10,
        includeMetadata: true,
      })
    )
      .pipe(
        switchMap((transformationResult) => {
          if (!transformationResult.success) {
            this.snackbarService.showError(
              this.translate.instant(
                'global.messages.profile.file_transformation_error'
              )
            );
            return of(null);
          }

          const formData = FormUtils.getFormValueWithDefaults(this.formGroup);
          const clientBlobFiles = (transformationResult.data || []).map(
            (dto) => {
              const clientDto = new BlobFileDto();
              clientDto.fileName = dto.fileName;
              clientDto.base64Content = dto.base64Content;
              clientDto.contentType = dto.contentType;
              return clientDto;
            }
          );
          const updateCommand = ProfileFormFactory.createUpdateCommand(
            formData,
            clientBlobFiles
          );

          return this.partnerClient.employeeClient.updateEmployee(
            updateCommand
          );
        }),
        takeUntil(this.destroyed$),
        finalize(() => this.profileSubmitLoading.set(false))
      )
      .subscribe({
        next: (result) => {
          if (result) {
            this.snackbarService.showSuccess(
              this.translate.instant(
                'global.messages.profile.onboarding_submitted'
              )
            );
          }
        },
      });
  }

  // === Employee Documents Management ===

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
    } catch {
      this.documentsState.update((s) => ({ ...s, deleting: false }));
    }
  }

  downloadEmployeeDocument(documentId: string, fileName: string): void {
    this.partnerClient.employeeClient
      .downloadMyDocument(documentId)
      .pipe(
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
