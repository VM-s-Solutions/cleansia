import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import {
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  TableDefinition,
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
  styleUrl: './email-template-list.component.scss',
})
export class EmailTemplateListComponent implements OnInit, OnDestroy {
  readonly facade = inject(EmailTemplateListFacade);
  private readonly translate = inject(TranslateService);

  tableDefinition!: TableDefinition<EmailTypeListItemDto>;

  ngOnInit(): void {
    this.tableDefinition = getEmailTypeTableDefinition(
      {
        onViewDetail: (row) => this.facade.navigateToDetail(row.emailType!),
      },
      this.translate
    );
    this.facade.loadEmailTypes();
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }
}
