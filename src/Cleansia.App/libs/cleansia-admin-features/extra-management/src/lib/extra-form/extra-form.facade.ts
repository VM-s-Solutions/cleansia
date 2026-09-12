import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminCurrencyListItem,
  AdminExtraDetailDto,
  CreateExtraCommand,
  CreateExtraResponse,
  CreateExtraTranslationInput,
  LanguageListItem,
  UpdateExtraCommand,
  UpdateExtraResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface LanguageOption {
  code: string;
  name: string;
}

export interface CurrencyOption {
  code: string;
  symbol: string;
  name: string;
  /**
   * Whether the platform OPERATES in this currency. The backend refuses a catalogue entry that is not
   * priced in every active currency, and permits — deliberately — a price in an inactive one, because
   * pricing a market before opening it is how a currency gets switched on at all. The form has to say
   * which is which, or the admin learns it from a rejected save.
   */
  isActive: boolean;
}

export interface ExtraFormData {
  /** Create only. Immutable after creation -- UpdateExtraCommand has no slug property. */
  slug: string;
  name: string;
  description: string;
  displayOrder: number;
  /** Price per currency CODE. A blank block is ABSENT, never zero -- see ServiceFormData.prices. */
  prices: { [key: string]: number };
  translations: { [key: string]: { name: string; description: string } };
}

@Injectable()
export class ExtraFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly extra = signal<AdminExtraDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly languages = signal<LanguageOption[]>([]);
  readonly currencies = signal<CurrencyOption[]>([]);

  loadExtra(extraId: string): void {
    this.loading.set(true);

    this.adminClient.adminExtraClient
      .details(extraId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.extra.set(response);
        } else {
          this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT]);
        }
      });
  }

  loadLanguages(): void {
    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as LanguageListItem[]))
      )
      .subscribe((languages: LanguageListItem[]) => {
        // `?? []` because the generated client answers a 200 whose body is not a JSON array — and a
        // 204 — with NULL, not an empty list: `processGetOverview` in admin-client.ts falls past both
        // branches to `result200 = null as any` while its declared type promises an array. Nothing
        // above catches it — null is not an error, so `catchError` never fires, and TypeScript never
        // complains, because the declared type is non-nullable. The `.filter` on the next line is
        // where that null stops being invisible.
        this.languages.set(
          (languages ?? [])
            .filter((lang: LanguageListItem): lang is LanguageListItem & { code: string; name: string } =>
              Boolean(lang.code) && Boolean(lang.name))
            .map((lang: LanguageListItem) => ({
              code: lang.code!,
              name: lang.name!,
            }))
        );
      });
  }

  /**
   * The currencies a price block is rendered for. The backend refuses an extra that is not priced
   * in every currency the platform operates in, so this list is what the admin has to fill in.
   */
  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as AdminCurrencyListItem[]))
      )
      .subscribe((currencies: AdminCurrencyListItem[]) => {
        // The same generated-client null as `loadLanguages` above.
        this.currencies.set(
          (currencies ?? [])
            .filter(
              (c): c is AdminCurrencyListItem & { code: string } => Boolean(c.code)
            )
            .map((c) => ({
              code: c.code,
              symbol: c.symbol ?? c.code,
              name: c.name ?? c.code,
              isActive: c.isActive,
            }))
        );
      });
  }

  createExtra(data: ExtraFormData): void {
    this.saving.set(true);

    const command = new CreateExtraCommand();
    command.slug = data.slug;
    command.name = data.name;
    command.description = data.description || undefined;
    command.displayOrder = data.displayOrder;
    command.prices = data.prices;
    command.translations = this.buildTranslations(data.translations);

    this.adminClient.adminExtraClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: CreateExtraResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.extra_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT]);
        }
      });
  }

  updateExtra(extraId: string, data: ExtraFormData): void {
    this.saving.set(true);

    const command = new UpdateExtraCommand();
    command.extraId = extraId;
    command.name = data.name;
    command.description = data.description || undefined;
    command.displayOrder = data.displayOrder;
    command.prices = data.prices;
    command.translations = this.buildTranslations(data.translations);

    this.adminClient.adminExtraClient
      .update(extraId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdateExtraResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.extra_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT]);
        }
      });
  }

  private buildTranslations(source: {
    [key: string]: { name: string; description: string };
  }): { [key: string]: CreateExtraTranslationInput } {
    const translations: { [key: string]: CreateExtraTranslationInput } = {};
    for (const [lang, trans] of Object.entries(source)) {
      if (trans.name || trans.description) {
        const translation = new CreateExtraTranslationInput();
        translation.name = trans.name;
        translation.description = trans.description || undefined;
        translations[lang] = translation;
      }
    }
    return translations;
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT]);
  }
}
