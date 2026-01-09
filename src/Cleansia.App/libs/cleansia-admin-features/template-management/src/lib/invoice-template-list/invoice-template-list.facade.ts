import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  GetPagedInvoiceTemplatesRequest,
  InvoiceTemplateFilter,
  InvoiceTemplateListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface InvoiceTemplateFilterParams {
  searchTerm?: string;
  countryId?: string;
  languageId?: string;
  isActive?: boolean;
}

@Injectable()
export class InvoiceTemplateListFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly templates = signal<InvoiceTemplateListItem[]>([]);
  readonly loading = signal<boolean>(false);

  private currentFilter = signal<InvoiceTemplateFilterParams | null>(null);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadTemplates(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    const filter = new InvoiceTemplateFilter();
    if (filterParams?.searchTerm) {
      filter.searchTerm = filterParams.searchTerm;
    }
    if (filterParams?.countryId) {
      filter.countryId = filterParams.countryId;
    }
    if (filterParams?.languageId) {
      filter.languageId = filterParams.languageId;
    }
    if (filterParams?.isActive !== undefined) {
      filter.isActive = filterParams.isActive;
    }

    const request = new GetPagedInvoiceTemplatesRequest({
      offset: 0,
      limit: 1000,
      filter: filter,
      sort: this.currentSort(),
    });

    this.adminClient.adminInvoiceTemplateClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.load_error')
          );
          console.error('Error loading invoice templates:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.templates.set(response.data || []);
        }
      });
  }

  onSortChange(event: { field: string; order: number }): void {
    const sort = new SortDefinition({
      field: event.field,
      direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
    });
    this.currentSort.set([sort]);
    this.loadTemplates();
  }

  applyFilter(filter: InvoiceTemplateFilterParams): void {
    this.currentFilter.set(filter);
    this.loadTemplates();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.loadTemplates();
  }

  navigateToCreate(): void {
    this.router.navigate(['/template-management', 'invoice-templates', 'create']);
  }

  navigateToEdit(template: InvoiceTemplateListItem): void {
    if (template.id) {
      this.router.navigate(['/template-management', 'invoice-templates', template.id, 'edit']);
    }
  }

  activateTemplate(template: InvoiceTemplateListItem): void {
    if (!template.id) return;

    this.adminClient.adminInvoiceTemplateClient
      .activate(template.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.activate_error')
          );
          console.error('Error activating template:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.activate_success')
          );
          this.loadTemplates();
        }
      });
  }

  deactivateTemplate(template: InvoiceTemplateListItem): void {
    if (!template.id) return;

    this.adminClient.adminInvoiceTemplateClient
      .deactivate(template.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.deactivate_error')
          );
          console.error('Error deactivating template:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.deactivate_success')
          );
          this.loadTemplates();
        }
      });
  }

  deleteTemplate(template: InvoiceTemplateListItem): void {
    if (!template.id) return;

    this.adminClient.apiClient
      .adminInvoiceTemplateDelete(template.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.template_management.messages.delete_error')
          );
          console.error('Error deleting template:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.template_management.messages.delete_success')
          );
          this.loadTemplates();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
