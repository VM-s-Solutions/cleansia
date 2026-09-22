import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminClient, LanguageListItem } from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class LanguageManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly lang = currentLanguage(this.translate);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  // The overview is one short master list, so the search filters it here rather than on the server.
  private readonly allLanguages = signal<LanguageListItem[]>([]);
  private readonly searchTerm = signal('');
  readonly languages = computed(() => {
    const term = this.searchTerm().trim().toLocaleLowerCase();
    const all = this.allLanguages();
    if (!term) return all;
    return all.filter(
      (language) =>
        language.code?.toLocaleLowerCase().includes(term) ||
        language.name?.toLocaleLowerCase().includes(term)
    );
  });

  readonly filterForm = inject(FormBuilder).group({
    searchTerm: [''],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] =>
      value.searchTerm
        ? [
            {
              key: 'searchTerm',
              label: this.translate.instant('pages.language_management.filters.search'),
              value: value.searchTerm,
            },
          ]
        : [],
    apply: (value) => this.searchTerm.set(value.searchTerm ?? ''),
  });

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadLanguages(): void {
    this.loading.set(true);

    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([])),
        finalize(() => this.loading.set(false))
      )
      .subscribe((languages) => {
        // `?? []` — the generated client can put a NULL in a signal typed as an array; reasoned out
        // in service-management/service-form.facade.ts. Nothing dereferences it here, so the null
        // would travel as far as the language table before it threw.
        this.allLanguages.set(languages ?? []);
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
    const id = language.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.language_management.delete_confirm',
        'pages.language_management.delete_language',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deleteLanguageConfirmed(id));
  }

  private deleteLanguageConfirmed(id: string): void {
    this.adminClient.adminLanguageClient
      .delete(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.language_management.messages.delete_success'
          );
          this.loadLanguages();
        }
      });
  }
}
