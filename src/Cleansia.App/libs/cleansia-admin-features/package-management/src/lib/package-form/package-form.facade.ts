import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminPackageDetailDto,
  CreatePackageCommand,
  CreatePackageResponse,
  CurrencyListItem,
  PackageTranslationInput,
  LanguageListItem,
  ServiceListItem,
  UpdatePackageCommand,
  UpdatePackageResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CleansiaAdminRoute,
  SnackbarService,
  extractApiErrorCode,
} from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  DerivedServiceGross,
  PACKAGE_ERROR_KEY_MAP,
  PACKAGE_FALLBACK_ERROR_KEY,
  PackageServiceWeightRow,
  deriveServiceGrosses,
} from './package-form.models';

export interface LanguageOption {
  code: string;
  name: string;
}

export interface CurrencyOption {
  code: string;
  symbol: string;
  name: string;
  isDefault: boolean;
}

export interface PackageFormData {
  name: string;
  description: string;
  tagline: string;
  isPopular: boolean;
  /**
   * Price per currency CODE. A currency the admin left blank is absent rather than present at zero
   * — the backend upserts a row for every key it receives, so a zero would put the package on sale
   * for nothing instead of leaving it unpriced in that market.
   */
  prices: { [key: string]: number };
  serviceIds: string[];
  translations: {
    [key: string]: { name: string; description: string; tagline: string };
  };
}

const DEFAULT_WEIGHT = 1;

@Injectable()
export class PackageFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly pkg = signal<AdminPackageDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);
  readonly languages = signal<LanguageOption[]>([]);
  readonly currencies = signal<CurrencyOption[]>([]);
  readonly availableServices = signal<ServiceListItem[]>([]);

  /**
   * Which currency the per-service gross preview is denominated in. The preview splits ONE number
   * across the included services by weight, so it has to pick a currency, and the default is the
   * only defensible pick: it is the one the platform is certainly selling in.
   */
  readonly defaultCurrencyCode = computed<string | null>(
    () => this.currencies().find((c) => c.isDefault)?.code ?? null
  );

  readonly weightRows = signal<PackageServiceWeightRow[]>([]);
  readonly price = signal<number>(0);

  readonly derivedGrosses = computed<DerivedServiceGross[]>(() =>
    deriveServiceGrosses(this.weightRows(), this.price())
  );

  setPrice(price: number): void {
    this.price.set(Number.isFinite(price) ? price : 0);
  }

  syncWeightRows(
    selected: ReadonlyArray<{ id: string; name: string }>,
    source?: ReadonlyArray<{ id?: string; priceWeight?: number }>
  ): void {
    const current = new Map(this.weightRows().map((row) => [row.id, row.weight]));
    const seeded = new Map(
      (source ?? [])
        .filter((item): item is { id: string; priceWeight: number } =>
          Boolean(item.id)
        )
        .map((item) => [item.id, item.priceWeight ?? DEFAULT_WEIGHT])
    );

    this.weightRows.set(
      selected.map((service) => ({
        id: service.id,
        name: service.name,
        weight:
          current.get(service.id) ??
          seeded.get(service.id) ??
          DEFAULT_WEIGHT,
      }))
    );
  }

  setWeight(serviceId: string, weight: number): void {
    const normalized =
      Number.isFinite(weight) && weight > 0 ? weight : DEFAULT_WEIGHT;
    this.weightRows.update((rows) =>
      rows.map((row) =>
        row.id === serviceId ? { ...row, weight: normalized } : row
      )
    );
  }

  buildServiceWeights(): { [serviceId: string]: number } {
    return this.weightRows().reduce<{ [serviceId: string]: number }>(
      (map, row) => {
        map[row.id] = row.weight > 0 ? row.weight : DEFAULT_WEIGHT;
        return map;
      },
      {}
    );
  }

  loadPackage(packageId: string): void {
    this.loading.set(true);

    this.adminClient.adminPackageClient
      .details(packageId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.pkg.set(response);
        } else {
          this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT]);
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
        // `?? []` because the generated client returns NULL, not an empty list, for a 200 whose body
        // is not a JSON array and for a 204, while its declared type promises an array — neither
        // `catchError` nor the compiler can see it. Reasoned out in full in
        // service-management/service-form.facade.ts; the `.filter` below is this file's crash site.
        this.languages.set(
          (languages ?? [])
            .filter(
              (
                lang: LanguageListItem
              ): lang is LanguageListItem & { code: string; name: string } =>
                Boolean(lang.code) && Boolean(lang.name)
            )
            .map((lang: LanguageListItem) => ({
              code: lang.code!,
              name: lang.name!,
            }))
        );
      });
  }

  /**
   * The currencies a price block is rendered for -- see the service form's twin. The backend refuses
   * a package that is not priced in every currency the platform operates in.
   */
  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as CurrencyListItem[]))
      )
      .subscribe((currencies: CurrencyListItem[]) => {
        // The same generated-client null as `loadLanguages` above.
        this.currencies.set(
          (currencies ?? [])
            .filter(
              (c): c is CurrencyListItem & { code: string } => Boolean(c.code)
            )
            .map((c) => ({
              code: c.code,
              symbol: c.symbol ?? c.code,
              name: c.name ?? c.code,
              isDefault: c.isDefault,
            }))
        );
      });
  }

  loadAvailableServices(): void {
    this.adminClient.adminServiceClient
      .getPaged(undefined, undefined, undefined, 0, 1000)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response?.data) {
          this.availableServices.set(response.data);
        }
      });
  }

  createPackage(data: PackageFormData): void {
    this.saving.set(true);
    this.errorKey.set(null);

    const command = new CreatePackageCommand();
    command.name = data.name;
    command.description = data.description;
    command.tagline = data.tagline || undefined;
    command.isPopular = data.isPopular;
    command.prices = data.prices;
    command.serviceIds = data.serviceIds;
    command.translations = this.buildTranslations(data.translations);

    this.adminClient.adminPackageClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.errorKey.set(this.resolveErrorKey(error));
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: CreatePackageResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.package_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT]);
        } else {
          this.snackbarService.showError(
            this.translate.instant(this.errorKey() ?? PACKAGE_FALLBACK_ERROR_KEY)
          );
        }
      });
  }

  updatePackage(packageId: string, data: PackageFormData): void {
    this.saving.set(true);
    this.errorKey.set(null);

    const command = new UpdatePackageCommand();
    command.packageId = packageId;
    command.name = data.name;
    command.description = data.description;
    command.tagline = data.tagline || undefined;
    command.isPopular = data.isPopular;
    command.prices = data.prices;
    command.serviceIds = data.serviceIds;
    command.serviceWeights = this.buildServiceWeights();
    command.translations = this.buildTranslations(data.translations);

    this.adminClient.adminPackageClient
      .update(packageId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.errorKey.set(this.resolveErrorKey(error));
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdatePackageResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.package_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT]);
        } else {
          this.snackbarService.showError(
            this.translate.instant(this.errorKey() ?? PACKAGE_FALLBACK_ERROR_KEY)
          );
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT]);
  }

  private buildTranslations(source: {
    [key: string]: { name: string; description: string; tagline: string };
  }): { [key: string]: PackageTranslationInput } {
    const translations: { [key: string]: PackageTranslationInput } = {};
    for (const [lang, trans] of Object.entries(source)) {
      if (trans.name || trans.description || trans.tagline) {
        const translation = new PackageTranslationInput();
        translation.name = trans.name;
        translation.description = trans.description;
        translation.tagline = trans.tagline || undefined;
        translations[lang] = translation;
      }
    }
    return translations;
  }

  private resolveErrorKey(error: unknown): string {
    const code = extractApiErrorCode(error);
    if (code && PACKAGE_ERROR_KEY_MAP[code]) {
      return PACKAGE_ERROR_KEY_MAP[code];
    }
    return PACKAGE_FALLBACK_ERROR_KEY;
  }
}
