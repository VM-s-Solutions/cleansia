import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { AdminCustomerListItem } from '@cleansia/admin-services';
import {
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CustomersListFacade } from './customers-list.facade';
import { getCustomerTableDefinition } from './customers-list.models';

@Component({
  selector: 'cleansia-admin-customers-list',
  standalone: true,
  imports: [
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './customers-list.component.html',
  providers: [CustomersListFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomersListComponent implements OnInit {
  protected readonly facade = inject(CustomersListFacade);
  private readonly translate = inject(TranslateService);

  private readonly statusTemplate = viewChild<TemplateRef<AdminCustomerListItem>>('statusTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return getCustomerTableDefinition(
      { onView: (row) => this.facade.openCustomer(row) },
      this.translate,
      this.statusTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadCustomers();
  }
}
