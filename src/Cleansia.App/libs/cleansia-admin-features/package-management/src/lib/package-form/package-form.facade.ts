import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminPackageDetailDto,
  CreatePackageCommand,
  CreatePackageResponse,
  CreateServiceTranslationInput,
  LanguageListItem,
  ServiceListItem,
  UpdatePackageCommand,
  UpdatePackageResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
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

export interface PackageFormData {
  name: string;
  description: string;
  price: number;
  serviceIds: string[];
  translations: { [key: string]: { name: string; description: string } };
}

interface ApiErrorResult {
  detail?: string;
  title?: string;
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
  readonly availableServices = signal<ServiceListItem[]>([]);

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
        this.languages.set(
          languages
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

  loadAvailableServices(): void {
    this.adminClient.adminServiceClient
      .getPaged(
        undefined, // searchTerm
        undefined, // sort
        0, // offset
        1000 // limit - Load all services for selection
      )
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

    const command = new CreatePackageCommand({
      name: data.name,
      description: data.description,
      price: data.price,
      serviceIds: data.serviceIds,
      translations: this.buildTranslations(data.translations),
    });

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

    const command = new UpdatePackageCommand({
      packageId,
      name: data.name,
      description: data.description,
      price: data.price,
      serviceIds: data.serviceIds,
      serviceWeights: this.buildServiceWeights(),
      translations: this.buildTranslations(data.translations),
    });

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
    [key: string]: { name: string; description: string };
  }): { [key: string]: CreateServiceTranslationInput } {
    const translations: { [key: string]: CreateServiceTranslationInput } = {};
    for (const [lang, trans] of Object.entries(source)) {
      if (trans.name || trans.description) {
        translations[lang] = new CreateServiceTranslationInput({
          name: trans.name,
          description: trans.description,
        });
      }
    }
    return translations;
  }

  private resolveErrorKey(error: unknown): string {
    const apiError = error as { result?: ApiErrorResult; response?: string };
    let code = apiError?.result?.detail || apiError?.result?.title;

    if (!code && apiError?.response) {
      try {
        const parsed = JSON.parse(apiError.response) as ApiErrorResult;
        code = parsed.detail || parsed.title;
      } catch {
        code = undefined;
      }
    }

    if (code && PACKAGE_ERROR_KEY_MAP[code]) {
      return PACKAGE_ERROR_KEY_MAP[code];
    }
    return PACKAGE_FALLBACK_ERROR_KEY;
  }
}
