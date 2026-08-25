import { Injectable, computed, inject, signal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  BlobFileDto,
  GetCountryFieldLabelsCountryFieldLabelsDto,
  PartnerClient,
} from '@cleansia/partner-services';
import {
  FileValidationErrorService,
  SnackbarService,
} from '@cleansia/services';
import { checkEmployeeCurrent } from '@cleansia/partner-stores';
import { FileTransformationUtils, FormUtils } from '@cleansia/utils';
import { Store } from '@ngrx/store';
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
import { ProfileFormData, ProfileFormFactory } from './profile.models';
import { ProfileBankFacade } from './profile-bank.facade';
import { ProfileDocumentsFacade } from './profile-documents.facade';
import { ProfileJobRadiusFacade } from './profile-job-radius.facade';

@Injectable()
export class ProfileFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly fileValidationErrorService = inject(
    FileValidationErrorService
  );
  private readonly store = inject(Store);

  readonly documentsFacade = inject(ProfileDocumentsFacade);
  readonly bankFacade = inject(ProfileBankFacade);
  readonly jobRadiusFacade = inject(ProfileJobRadiusFacade);

  readonly formGroup: FormGroup =
    ProfileFormFactory.createEmployeeProfileForm();

  profileLoading = signal(false);
  profileSubmitLoading = signal(false);
  countries = signal<ICleansiaSelectOption[]>([]);

  /// What the cleaner's country calls its business identifiers.
  ///
  /// `CountryConfiguration` has carried these since it was seeded and nothing read them, so this app
  /// hardcoded the Czech "IČO" in its own translation files — which a Polish or Ukrainian partner then
  /// read, and Poland has no IČO. Null means the platform holds no configuration for the country and
  /// the field falls back to its own neutral wording. Both mobile apps already do this.
  fieldLabels = signal<GetCountryFieldLabelsCountryFieldLabelsDto | null>(null);

  /// The country the labels on screen belong to. Guards against a slow answer for an earlier pick
  /// overwriting a newer one when the picker is changed twice.
  private fieldLabelsFor: string | null = null;
  // Display only — the signed-in partner's login address. It is deliberately not a form control:
  // the onboarding command carries no email, so anything typed here could never be saved.
  email = signal('');

  private profileData$: Observable<any> | null = null;

  /**
   * Silent on failure and silent on 404. A country we hold no configuration for is one whose word for
   * this we do not know — the neutral fallback is the honest answer, and an error banner would report
   * a missing translation as a broken form.
   */
  loadFieldLabels(countryId: string | null): void {
    if (!countryId || this.fieldLabelsFor === countryId) {
      return;
    }

    this.fieldLabelsFor = countryId;
    this.partnerClient.countryClient
      .getFieldLabels(countryId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((labels) => {
        // Only publish if the picker has not moved on since this call was made.
        if (this.fieldLabelsFor === countryId) {
          this.fieldLabels.set(labels);
        }
      });
  }

  loadProfile(): void {
    if (this.profileData$) {
      return;
    }

    this.profileLoading.set(true);

    const employee$ = this.partnerClient.employeeClient.getCurrentEmployee();
    // Partner profile only renders the country picker for the cleaner's
    // home address — they must live in a country we operate in. See
    // planning/active/service-areas.md (Session 4).
    const countries$ = this.partnerClient.countryClient.getServiced();

    this.profileData$ = combineLatest([employee$, countries$]).pipe(
      tap(([employee, countries]) => {
        const formData = ProfileFormFactory.mapEmployeeToFormData(employee);
        FormUtils.safePatchValue(this.formGroup, formData);
        this.email.set(employee.email ?? '');
        this.jobRadiusFacade.seed(employee);

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
        this.loadFieldLabels(employee.countryId ?? null);
        this.profileLoading.set(false);
      }),
      catchError(() => {
        this.profileLoading.set(false);
        this.profileData$ = null;
        this.jobRadiusFacade.markUnavailable();
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

          const formData = FormUtils.getFormValueWithDefaults<ProfileFormData>(this.formGroup);
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
            this.store.dispatch(checkEmployeeCurrent());
          }
        },
      });
  }
}
