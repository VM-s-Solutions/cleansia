import { Injectable, inject, signal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  BlobFileDto,
  Client,
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

@Injectable()
export class ProfileFacade extends UnsubscribeControlDirective {
  private readonly client = inject(Client);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly fileValidationErrorService = inject(
    FileValidationErrorService
  );

  readonly formGroup: FormGroup =
    ProfileFormFactory.createEmployeeProfileForm();

  profileLoading = signal(false);
  profileSubmitLoading = signal(false);
  countries = signal<ICleansiaSelectOption[]>([]);

  private profileData$: Observable<any> | null = null;

  loadProfile(): void {
    if (this.profileData$) {
      return;
    }

    this.profileLoading.set(true);

    const employee$ = this.client.employeeClient.getCurrentEmployee();
    const countries$ = this.client.countryClient.getOverview();

    this.profileData$ = combineLatest([employee$, countries$]).pipe(
      tap(([employee, countries]) => {
        const formData = ProfileFormFactory.mapEmployeeToFormData(employee);
        FormUtils.safePatchValue(this.formGroup, formData);

        const countryOptions: ICleansiaSelectOption[] = countries.map(
          (country) => {
            const translation =
              country.translations &&
              country.translations[this.translate.currentLang];
            return {
              label:
                typeof translation === 'string' ? translation : country.name!,
              value: country.id!,
            };
          }
        );

        this.countries.set(countryOptions);
        this.profileLoading.set(false);
      }),
      catchError((error) => {
        this.profileLoading.set(false);
        this.snackbarService.showErrorTranslated(
          'global.messages.profile.load_error'
        );
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

  onDocumentUpload(event: any): void {
    const files = event.target?.files || event.files;
    const normalizedFiles = FileTransformationUtils.normalizeFiles(files);

    if (!normalizedFiles.length) {
      this.snackbarService.showErrorTranslated(
        'global.messages.profile.no_files_selected'
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

    this.snackbarService.showSuccessTranslated(
      'global.messages.profile.documents_uploaded'
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

      this.snackbarService.showSuccessTranslated(
        'global.messages.profile.file_removed'
      );
    }
  }

  onSubmit(): void {
    if (!this.formGroup.valid) {
      this.snackbarService.showErrorTranslated(
        'global.messages.profile.fill_required_fields'
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
            this.snackbarService.showErrorTranslated(
              'global.messages.profile.file_transformation_error'
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

          return this.client.employeeClient.updateEmployee(updateCommand);
        }),
        takeUntil(this.destroyed$),
        finalize(() => this.profileSubmitLoading.set(false)),
        catchError(() => {
          this.snackbarService.showErrorTranslated(
            'global.messages.profile.submission_error'
          );
          return of(null);
        })
      )
      .subscribe({
        next: (result) => {
          if (result) {
            this.snackbarService.showSuccessTranslated(
              'global.messages.profile.onboarding_submitted'
            );
          }
        },
      });
  }
}
