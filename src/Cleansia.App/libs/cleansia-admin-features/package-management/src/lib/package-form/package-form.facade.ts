import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminPackageDetailDto,
  CreatePackageCommand,
  CreatePackageResponse,
  CreateServiceTranslationInput,
  GetPagedServicesRequest,
  LanguageListItem,
  ServiceFilter,
  ServiceListItem,
  UpdatePackageCommand,
  UpdatePackageResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

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

@Injectable()
export class PackageFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly pkg = signal<AdminPackageDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly languages = signal<LanguageOption[]>([]);
  readonly availableServices = signal<ServiceListItem[]>([]);

  loadPackage(packageId: string): void {
    this.loading.set(true);

    this.adminClient.adminPackageClient
      .details(packageId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.package_form.messages.load_error')
          );
          console.error('Error loading package:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.pkg.set(response);
        } else {
          this.router.navigate(['/package-management']);
        }
      });
  }

  loadLanguages(): void {
    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          console.error('Error loading languages:', error);
          return of([] as LanguageListItem[]);
        })
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

  loadAvailableServices(): void {
    const request = new GetPagedServicesRequest({
      offset: 0,
      limit: 1000, // Load all services for selection
      filter: new ServiceFilter(),
      sort: undefined,
    });

    this.adminClient.adminServiceClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          console.error('Error loading services:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response?.data) {
          this.availableServices.set(response.data);
        }
      });
  }

  createPackage(data: PackageFormData): void {
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

    const command = new CreatePackageCommand({
      name: data.name,
      description: data.description,
      price: data.price,
      serviceIds: data.serviceIds,
      translations,
    });

    this.adminClient.apiClient
      .adminPackagePost(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.package_form.messages.create_error')
          );
          console.error('Error creating package:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: CreatePackageResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.package_form.messages.create_success')
          );
          this.router.navigate(['/package-management']);
        }
      });
  }

  updatePackage(packageId: string, data: PackageFormData): void {
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

    const command = new UpdatePackageCommand({
      packageId,
      name: data.name,
      description: data.description,
      price: data.price,
      serviceIds: data.serviceIds,
      translations,
    });

    this.adminClient.apiClient
      .adminPackagePut(packageId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.package_form.messages.update_error')
          );
          console.error('Error updating package:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdatePackageResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.package_form.messages.update_success')
          );
          this.router.navigate(['/package-management']);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/package-management']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}