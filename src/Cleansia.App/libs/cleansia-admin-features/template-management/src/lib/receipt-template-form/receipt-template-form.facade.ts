import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CountryListItem,
  CreateReceiptTemplateCommand,
  LanguageListItem,
  ReceiptTemplateDetailDto,
  UpdateReceiptTemplateCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface ReceiptTemplateFormData {
  templateName: string;
  countryId: string;
  languageId: string;
  description: string | null;
  fileName: string | null;
  contentType: string | null;
  fileData: string | null;
}

@Injectable()
export class ReceiptTemplateFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly template = signal<ReceiptTemplateDetailDto | null>(null);
  readonly countries = signal<CountryListItem[]>([]);
  readonly languages = signal<LanguageListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly downloading = signal<boolean>(false);

  downloadTemplate(templateId: string, fileName?: string): void {
    this.downloading.set(true);

    this.adminClient.adminReceiptTemplateClient
      .download(templateId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.download_error')
          );
          console.error('Error downloading template:', error);
          return of(null);
        }),
        finalize(() => this.downloading.set(false))
      )
      .subscribe((response) => {
        if (response?.data) {
          const blob = response.data;
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = response.fileName || fileName || 'template.html';
          link.click();
          window.URL.revokeObjectURL(url);
        }
      });
  }

  openTemplateInNewTab(templateId: string): void {
    this.adminClient.adminReceiptTemplateClient
      .download(templateId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.download_error')
          );
          console.error('Error opening template:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response?.data) {
          const blob = response.data;
          const url = window.URL.createObjectURL(blob);
          window.open(url, '_blank');
        }
      });
  }

  loadTemplate(templateId: string): void {
    this.loading.set(true);

    this.adminClient.adminReceiptTemplateClient
      .details(templateId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.load_error')
          );
          console.error('Error loading template:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((template) => {
        this.template.set(template);
      });
  }

  loadCountries(): void {
    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          console.error('Error loading countries:', error);
          return of([]);
        })
      )
      .subscribe((countries) => {
        this.countries.set(countries);
      });
  }

  loadLanguages(): void {
    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          console.error('Error loading languages:', error);
          return of([]);
        })
      )
      .subscribe((languages) => {
        this.languages.set(languages);
      });
  }

  createTemplate(data: ReceiptTemplateFormData): void {
    this.saving.set(true);

    const command = new CreateReceiptTemplateCommand({
      templateName: data.templateName,
      countryId: data.countryId,
      languageId: data.languageId,
      description: data.description ?? undefined,
      fileName: data.fileName ?? undefined,
      contentType: data.contentType ?? undefined,
      fileData: data.fileData ?? undefined,
    });

    this.adminClient.apiClient
      .adminReceiptTemplatePost(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.save_error')
          );
          console.error('Error creating template:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.create_success')
          );
          this.router.navigate(['/template-management', 'receipt-templates']);
        }
      });
  }

  updateTemplate(templateId: string, data: ReceiptTemplateFormData): void {
    this.saving.set(true);

    const command = new UpdateReceiptTemplateCommand({
      receiptTemplateId: templateId,
      templateName: data.templateName,
      description: data.description ?? undefined,
      fileName: data.fileName ?? undefined,
      contentType: data.contentType ?? undefined,
      fileData: data.fileData ?? undefined,
    });

    this.adminClient.apiClient
      .adminReceiptTemplatePut(templateId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.save_error')
          );
          console.error('Error updating template:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.save_success')
          );
          this.router.navigate(['/template-management', 'receipt-templates']);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/template-management', 'receipt-templates']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
