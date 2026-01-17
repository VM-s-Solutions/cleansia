import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, LanguageListItem } from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class LanguageManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly languages = signal<LanguageListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadLanguages(): void {
    this.loading.set(true);

    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of([])),
        finalize(() => this.loading.set(false))
      )
      .subscribe((languages) => {
        this.languages.set(languages);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  navigateToCreateLanguage(): void {
    this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT, 'create']);
  }

  navigateToEditLanguage(language: LanguageListItem): void {
    if (language.id) {
      this.router.navigate([CleansiaAdminRoute.LANGUAGE_MANAGEMENT, language.id, 'edit']);
    }
  }

  deleteLanguage(language: LanguageListItem): void {
    if (!language.id) return;

    this.adminClient.adminLanguageClient
      .delete(language.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.language_management.messages.delete_success'
            )
          );
          this.loadLanguages();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
