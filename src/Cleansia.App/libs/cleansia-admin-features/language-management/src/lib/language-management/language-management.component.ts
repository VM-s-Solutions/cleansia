import { NgClass } from '@angular/common';
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
import { LanguageListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageManagementFacade } from './language-management.facade';
import {
  getLanguageTableDefinition,
  getLanguageToCountryCode,
} from './language-management.models';

@Component({
  selector: 'cleansia-admin-language-management',
  standalone: true,
  imports: [
    NgClass,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
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
  templateUrl: './language-management.component.html',
  providers: [LanguageManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageManagementComponent implements OnInit {
  protected readonly facade = inject(LanguageManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  private readonly flagTemplate = viewChild<TemplateRef<LanguageListItem>>('flagTemplate');

  readonly getLanguageToCountryCode = getLanguageToCountryCode;

  protected readonly table = computed(() => {
    this.facade.lang();
    return getLanguageTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEditLanguage(row),
        onDelete: (row) => this.confirmDeleteLanguage(row),
      },
      this.translate,
      this.permissions,
      this.flagTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadLanguages();
  }

  confirmDeleteLanguage(language: LanguageListItem): void {
    this.facade.deleteLanguage(language);
  }
}
