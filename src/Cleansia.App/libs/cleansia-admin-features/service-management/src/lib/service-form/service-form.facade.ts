import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminServiceDetailDto,
  CategoryDto,
  CreateServiceCommand,
  CreateServiceResponse,
  CreateServiceTranslationInput,
  LanguageListItem,
  UpdateServiceCommand,
  UpdateServiceResponse,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface LanguageOption {
  code: string;
  name: string;
}

export interface ServiceFormData {
  name: string;
  description: string;
  basePrice: number;
  perRoomPrice: number;
  estimatedTime: number;
  /** Category id from the picker — required by the backend validator. */
  categoryId: string;
  translations: { [key: string]: { name: string; description: string } };
}

export interface CategoryOption {
  id: string;
  name: string;
}

@Injectable()
export class ServiceFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly service = signal<AdminServiceDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly languages = signal<LanguageOption[]>([]);
  readonly categories = signal<CategoryOption[]>([]);

  loadService(serviceId: string): void {
    this.loading.set(true);

    this.adminClient.adminServiceClient
      .details(serviceId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.service.set(response);
        } else {
          this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT]);
        }
      });
  }

  loadLanguages(): void {
    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of([] as LanguageListItem[]))
      )
      .subscribe((languages: LanguageListItem[]) => {
        this.languages.set(
          languages
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
   * Fetch service categories for the form's category picker. Required for
   * create — backend validator rejects services with a missing categoryId.
   * Falls back to empty list on failure (form will block save).
   */
  loadCategories(): void {
    this.adminClient.adminServiceClient
      .categories()
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of([] as CategoryDto[]))
      )
      .subscribe((categories: CategoryDto[]) => {
        this.categories.set(
          categories
            .filter((c): c is CategoryDto & { id: string; name: string } =>
              Boolean(c.id) && Boolean(c.name))
            .map((c) => ({ id: c.id!, name: c.name! }))
        );
      });
  }

  createService(data: ServiceFormData): void {
    this.saving.set(true);

    const translations: { [key: string]: CreateServiceTranslationInput } = {};
    for (const [lang, trans] of Object.entries(data.translations)) {
      if (trans.name || trans.description) {
        translations[lang] = new CreateServiceTranslationInput({
          name: trans.name,
          description: trans.description,
        });
      }
    }

    const command = new CreateServiceCommand({
      name: data.name,
      description: data.description,
      basePrice: data.basePrice,
      perRoomPrice: data.perRoomPrice,
      estimatedTime: data.estimatedTime,
      translations,
      categoryId: data.categoryId,
    });

    this.adminClient.adminServiceClient
      .create(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: CreateServiceResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.service_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT]);
        }
      });
  }

  updateService(serviceId: string, data: ServiceFormData): void {
    this.saving.set(true);

    const translations: { [key: string]: CreateServiceTranslationInput } = {};
    for (const [lang, trans] of Object.entries(data.translations)) {
      if (trans.name || trans.description) {
        translations[lang] = new CreateServiceTranslationInput({
          name: trans.name,
          description: trans.description,
        });
      }
    }

    const command = new UpdateServiceCommand({
      serviceId,
      name: data.name,
      description: data.description,
      basePrice: data.basePrice,
      perRoomPrice: data.perRoomPrice,
      estimatedTime: data.estimatedTime,
      translations,
      categoryId: data.categoryId,
    });

    this.adminClient.adminServiceClient
      .update(serviceId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdateServiceResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.service_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT]);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
