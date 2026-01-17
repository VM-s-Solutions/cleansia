import { ChangeDetectorRef, Component, OnDestroy, OnInit, TemplateRef, inject, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import {
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  TableColumn,
  TableAction,
} from '@cleansia/components';
import { EmailTypeListItemDto } from '@cleansia/admin-services';
import { EmailTemplateListFacade } from './email-template-list.facade';
import { getEmailTypeTableDefinition } from './email-template-list.models';

@Component({
  selector: 'lib-email-template-list',
  standalone: true,
  imports: [
    CommonModule,
    TranslateModule,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
  ],
  providers: [EmailTemplateListFacade],
  templateUrl: './email-template-list.component.html',
})
export class EmailTemplateListComponent implements OnInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  readonly facade = inject(EmailTemplateListFacade);
  private readonly translate = inject(TranslateService);
  private destroy$ = new Subject<void>();

  languagesTemplate = viewChild<TemplateRef<any>>('languagesTemplate');

  columns!: TableColumn<EmailTypeListItemDto>[];
  actions!: TableAction<EmailTypeListItemDto>[];

  ngOnInit(): void {
    this.rebuildTableDefinitions();
    this.facade.loadEmailTypes();

    // Rebuild tables when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
      });
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getEmailTypeTableDefinition(
      {
        onViewDetail: (row) => this.facade.navigateToDetail(row.emailType!),
      },
      this.translate,
      this.languagesTemplate()
    );
    this.columns = tableDef.columns;
    this.actions = tableDef.actions;
    this.cd.detectChanges();
  }

  getTranslatedLanguages(languages: string[] | undefined): string {
    if (!languages || languages.length === 0) return '';
    return languages
      .map(code => this.translate.instant(`global.languages.${code.toLowerCase()}`))
      .join(', ');
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }
}
