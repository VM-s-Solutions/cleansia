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
import { ServiceListItem } from '@cleansia/admin-services';
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
import { ServiceManagementFacade } from './service-management.facade';
import { getServiceTableDefinition } from './service-management.models';

@Component({
  selector: 'cleansia-admin-service-management',
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
    CleansiaPermissionDirective,
  ],
  templateUrl: './service-management.component.html',
  providers: [ServiceManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceManagementComponent implements OnInit {
  protected readonly facade = inject(ServiceManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  protected readonly table = computed(() => {
    this.facade.lang();
    return getServiceTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEditService(row),
        onDelete: (row) => this.confirmDeleteService(row),
        onDeactivate: (row) => this.confirmDeactivateService(row),
        onActivate: (row) => this.facade.activateService(row),
        getIsActiveFilter: () => this.facade.isActiveFilter(),
      },
      this.translate,
      this.permissions,
      (value) => this.facade.formatCurrency(value)
    );
  });

  ngOnInit(): void {
    this.facade.loadServices();
  }

  confirmDeactivateService(row: ServiceListItem): void {
    this.facade.deactivateService(row);
  }

  confirmDeleteService(row: ServiceListItem): void {
    this.facade.deleteService(row);
  }
}
