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
import { CompanyInfoListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CompanyInfoListFacade } from './company-info-list.facade';
import { getCompanyInfoTableDefinition } from './company-info-list.models';

@Component({
  selector: 'cleansia-admin-company-info-list',
  standalone: true,
  imports: [
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    ReactiveFormsModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './company-info-list.component.html',
  providers: [CompanyInfoListFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompanyInfoListComponent implements OnInit {
  protected readonly facade = inject(CompanyInfoListFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  private readonly statusTemplate = viewChild<TemplateRef<CompanyInfoListItem>>('statusTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return getCompanyInfoTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEdit(row),
        onDelete: (row) => this.confirmDeleteCompanyInfo(row),
      },
      this.translate,
      this.permissions,
      this.statusTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadCompanyInfos();
  }

  confirmDeleteCompanyInfo(companyInfo: CompanyInfoListItem): void {
    this.facade.deleteCompanyInfo(companyInfo);
  }
}
