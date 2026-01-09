import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateLanguageCommand,
  LanguageDetailDto,
  UpdateLanguageCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface LanguageFormData {
  code: string;
  name: string;
}

@Injectable()
export class LanguageFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly language = signal<LanguageDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadLanguage(languageId: string): void {
    this.loading.set(true);

    this.adminClient.adminLanguageClient
      .details(languageId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.language_form.messages.load_error')
          );
          console.error('Error loading language:', error);
          this.router.navigate(['/language-management']);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((language) => {
        if (language) {
          this.language.set(language);
        }
      });
  }

  createLanguage(data: LanguageFormData): void {
    this.saving.set(true);

    const command = new CreateLanguageCommand({
      code: data.code,
      name: data.name,
    });

    this.adminClient.apiClient
      .adminLanguagePost(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.language_form.messages.create_error')
          );
          console.error('Error creating language:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.language_form.messages.create_success')
          );
          this.router.navigate(['/language-management']);
        }
      });
  }

  updateLanguage(languageId: string, data: LanguageFormData): void {
    this.saving.set(true);

    const command = new UpdateLanguageCommand({
      languageId: languageId,
      name: data.name,
    });

    this.adminClient.apiClient
      .adminLanguagePut(languageId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.language_form.messages.update_error')
          );
          console.error('Error updating language:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.language_form.messages.update_success')
          );
          this.router.navigate(['/language-management']);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/language-management']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}