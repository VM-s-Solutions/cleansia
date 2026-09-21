import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PromoCodeListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {
  PromoCodeStatusFilter,
  PromoCodesListFacade,
} from './promo-codes-list.facade';
import {
  getPromoCodeStatus,
  getPromoCodeTableDefinition,
  PromoCodeStatusBadge,
} from './promo-codes-list.models';

@Component({
  selector: 'cleansia-admin-promo-codes-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './promo-codes-list.component.html',
  providers: [PromoCodesListFacade],
})
export class PromoCodesListComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  protected readonly facade = inject(PromoCodesListFacade);
  protected readonly Policy = Policy;

  private readonly destroy$ = new Subject<void>();

  private readonly statusTemplate = viewChild<TemplateRef<PromoCodeListItem>>('statusTemplate');

  promoCodeColumns!: TableColumn<PromoCodeListItem>[];
  promoCodeActions!: TableAction<PromoCodeListItem>[];

  filterForm = this.fb.group({
    searchCode: [''],
    status: ['all' as PromoCodeStatusFilter],
  });

  statusOptions = [
    { label: 'pages.promo_codes.status_filter_all', value: 'all' },
    { label: 'pages.promo_codes.status_filter_active', value: 'active' },
    { label: 'pages.promo_codes.status_filter_inactive', value: 'inactive' },
    { label: 'pages.promo_codes.status_filter_expired', value: 'expired' },
  ];

  get translatedStatusOptions(): { label: string; value: string }[] {
    return this.statusOptions.map((opt) => ({
      label: this.translate.instant(opt.label),
      value: opt.value,
    }));
  }

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();

    this.filterForm.controls.searchCode.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.filterForm.controls.status.valueChanges
      .pipe(distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.rebuildTableDefinitions());

    this.facade.loadPromoCodes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  private rebuildTableDefinitions(): void {
    const def = getPromoCodeTableDefinition(
      {
        onView: (row) => this.facade.navigateToDetail(row),
        onEdit: (row) => this.facade.navigateToEdit(row),
        onDeactivate: (row) => this.confirmDeactivate(row),
      },
      this.translate,
      this.permissions,
      (d?: Date) => this.formatDate(d),
      this.statusTemplate()
    );
    this.promoCodeColumns = def.columns;
    this.promoCodeActions = def.actions;
  }

  promoStatus(row: PromoCodeListItem): PromoCodeStatusBadge {
    return getPromoCodeStatus(row);
  }

  private formatDate(d?: Date): string {
    return formatDate(d, this.translate.currentLang) || '—';
  }

  applyFilters(): void {
    const v = this.filterForm.value;
    this.facade.applyFilter({
      searchCode: v.searchCode?.trim() || undefined,
      status: (v.status ?? 'all') as PromoCodeStatusFilter,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({ searchCode: '', status: 'all' });
    this.facade.resetFilter();
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  createPromoCode(): void {
    this.facade.navigateToCreate();
  }

  confirmDeactivate(promoCode: PromoCodeListItem): void {
    this.facade.deactivate(promoCode);
  }
}
