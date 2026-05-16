import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateLanguageCommand,
  LanguageDetailDto,
  UpdateLanguageCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface LanguageFormData {
  code: string;
  name: string;
}

@Injectable()
export class LanguageFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly language = signal<LanguageDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadLanguage(languageId: string): void {
    this.loading.set(true);

    this.adminClient.adminLanguageClient
      .details(languageId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT]);
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

    this.adminClient.adminLanguageClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.language_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT]);
        }
      });
  }

  updateLanguage(languageId: string, data: LanguageFormData): void {
    this.saving.set(true);

    const command = new UpdateLanguageCommand({
      languageId: languageId,
      name: data.name,
    });

    this.adminClient.adminLanguageClient
      .update(languageId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.language_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT]);
  }
}