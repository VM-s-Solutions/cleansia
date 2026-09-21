import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { PackageListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { PackageManagementFacade } from './package-management.facade';
import { getPackageTableDefinition } from './package-management.models';

@Component({
  selector: 'cleansia-admin-package-management',
  standalone: true,
  imports: [
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    ReactiveFormsModule,
    ConfirmDialogModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './package-management.component.html',
  providers: [PackageManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PackageManagementComponent implements OnInit {
  protected readonly facade = inject(PackageManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly table = computed(() => {
    this.facade.lang();
    return getPackageTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEditPackage(row),
        onDelete: (row) => this.confirmDeletePackage(row),
        onDeactivate: (row) => this.confirmDeactivatePackage(row),
        onActivate: (row) => this.facade.activatePackage(row),
        getIsActiveFilter: () => this.facade.isActiveFilter(),
      },
      this.translate,
      this.permissions,
      (value) => this.facade.formatCurrency(value)
    );
  });

  ngOnInit(): void {
    this.facade.loadPackages();
  }

  confirmDeactivatePackage(row: PackageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.package_management.deactivate_confirm', { name: row.name }),
      header: this.translate.instant('pages.package_management.deactivate_package'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deactivatePackage(row);
      },
    });
  }

  confirmDeletePackage(row: PackageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.package_management.delete_confirm'),
      header: this.translate.instant('pages.package_management.delete_package'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deletePackage(row);
      },
    });
  }
}
