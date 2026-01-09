import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  inject,
  OnDestroy,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  PackageListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { PackageManagementFacade } from './package-management.facade';
import { getPackageTableDefinition } from './package-management.models';

@Component({
  selector: 'cleansia-admin-package-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ConfirmDialogModule,
  ],
  templateUrl: './package-management.component.html',
  providers: [PackageManagementFacade, ConfirmationService],
})
export class PackageManagementComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(PackageManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  packageTableDefinition!: TableDefinition<PackageListItem>;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    searchTerm: [''],
  });

  ngAfterViewInit(): void {
    this.packageTableDefinition = getPackageTableDefinition(
      {
        onViewDetails: this.viewPackageDetails.bind(this),
        onEdit: this.editPackage.bind(this),
        onDelete: this.confirmDeletePackage.bind(this),
      },
      this.translate,
      this.facade.formatCurrency.bind(this.facade)
    );

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.facade.loadPackages();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewPackageDetails(pkg: PackageListItem): void {
    if (pkg.id) {
      this.router.navigate(['/package-management', pkg.id, 'edit']);
    }
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      searchTerm: formValues.searchTerm?.trim() || undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      searchTerm: '',
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    const sort = [
      new SortDefinition({
        field: event.field,
        direction: sortDirection,
      }),
    ];
    this.facade.onSortChange(sort);
  }

  createPackage(): void {
    this.facade.navigateToCreatePackage();
  }

  editPackage(pkg: PackageListItem): void {
    this.facade.navigateToEditPackage(pkg);
  }

  confirmDeletePackage(pkg: PackageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.package_management.delete_confirm'),
      header: this.translate.instant('pages.package_management.delete_package'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deletePackage(pkg);
      },
    });
  }
}