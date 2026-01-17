import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  ReceiptTemplateListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface ReceiptTemplateFilterParams {
  searchTerm?: string;
  countryId?: string;
  languageId?: string;
  isActive?: boolean;
}

@Injectable()
export class ReceiptTemplateListFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly templates = signal<ReceiptTemplateListItem[]>([]);
  readonly loading = signal<boolean>(false);

  private currentFilter = signal<ReceiptTemplateFilterParams | null>(null);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadTemplates(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminReceiptTemplateClient
      .getPaged(
        filterParams?.searchTerm,
        filterParams?.countryId,
        filterParams?.languageId,
        filterParams?.isActive,
        this.currentSort(),
        0, // offset
        1000 // limit
      )
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
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

  applyFilter(filter: ReceiptTemplateFilterParams): void {
    this.currentFilter.set(filter);
    this.loadTemplates();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.loadTemplates();
  }

  navigateToCreate(): void {
    this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, 'receipt-templates', 'create']);
  }

  navigateToEdit(template: ReceiptTemplateListItem): void {
    if (template.id) {
      this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, 'receipt-templates', template.id, 'edit']);
    }
  }

  activateTemplate(template: ReceiptTemplateListItem): void {
    if (!template.id) return;

    this.adminClient.adminReceiptTemplateClient
      .activate(template.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
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

  deactivateTemplate(template: ReceiptTemplateListItem): void {
    if (!template.id) return;

    this.adminClient.adminReceiptTemplateClient
      .deactivate(template.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
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

  deleteTemplate(template: ReceiptTemplateListItem): void {
    if (!template.id) return;

    this.adminClient.adminReceiptTemplateClient
      .delete(template.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
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
