import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, inject, OnDestroy } from '@angular/core';
import { LanguageListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Subject } from 'rxjs';
import { LanguageManagementFacade } from './language-management.facade';
import { getLanguageTableDefinition } from './language-management.models';

@Component({
  selector: 'cleansia-admin-language-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ConfirmDialogModule,
  ],
  templateUrl: './language-management.component.html',
  providers: [LanguageManagementFacade, ConfirmationService],
})
export class LanguageManagementComponent implements AfterViewInit, OnDestroy {
  protected readonly facade = inject(LanguageManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  languageTableDefinition!: TableDefinition<LanguageListItem>;

  private destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.languageTableDefinition = getLanguageTableDefinition(
      {
        onEdit: this.editLanguage.bind(this),
        onDelete: this.confirmDeleteLanguage.bind(this),
      },
      this.translate
    );

    this.facade.loadLanguages();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  createLanguage(): void {
    this.facade.navigateToCreateLanguage();
  }

  editLanguage(language: LanguageListItem): void {
    this.facade.navigateToEditLanguage(language);
  }

  confirmDeleteLanguage(language: LanguageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.language_management.delete_confirm'),
      header: this.translate.instant('pages.language_management.delete_language'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteLanguage(language);
      },
    });
  }
}