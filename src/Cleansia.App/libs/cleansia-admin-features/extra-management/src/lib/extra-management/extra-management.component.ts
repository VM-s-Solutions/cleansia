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
import { ExtraListItem } from '@cleansia/admin-services';
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
import { ExtraManagementFacade } from './extra-management.facade';
import { getExtraTableDefinition } from './extra-management.models';

@Component({
  selector: 'cleansia-admin-extra-management',
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
  templateUrl: './extra-management.component.html',
  providers: [ExtraManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtraManagementComponent implements OnInit {
  protected readonly facade = inject(ExtraManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly table = computed(() => {
    this.facade.lang();
    return getExtraTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEditExtra(row),
        onDelete: (row) => this.confirmDeleteExtra(row),
        onDeactivate: (row) => this.confirmDeactivateExtra(row),
        onActivate: (row) => this.facade.activateExtra(row),
        getIsActiveFilter: () => this.facade.isActiveFilter(),
      },
      this.translate,
      this.permissions,
      (value) => this.facade.formatCurrency(value)
    );
  });

  ngOnInit(): void {
    this.facade.loadExtras();
  }

  confirmDeactivateExtra(row: ExtraListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.extra_management.deactivate_confirm', { name: row.name }),
      header: this.translate.instant('pages.extra_management.deactivate_extra'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deactivateExtra(row);
      },
    });
  }

  confirmDeleteExtra(row: ExtraListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.extra_management.delete_confirm'),
      header: this.translate.instant('pages.extra_management.delete_extra'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteExtra(row);
      },
    });
  }
}
