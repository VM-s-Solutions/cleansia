import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
} from '@angular/core';
import {
  LegalDocumentAudience,
  LegalDocumentTextSummaryDto,
  LegalDocumentType,
  LegalDocumentVersionDto,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { LegalDocumentsFacade } from './legal-documents.facade';
import {
  formatEffectiveDate,
  getAudienceLabelKey,
  getLegalTextTableDefinition,
  getLegalVersionTableDefinition,
  getTypeLabelKey,
} from './legal-documents.models';

@Component({
  selector: 'cleansia-admin-legal-documents',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
  ],
  templateUrl: './legal-documents.component.html',
  providers: [LegalDocumentsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalDocumentsComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(LegalDocumentsFacade);

  versionColumns!: TableColumn<LegalDocumentVersionDto>[];
  versionActions!: TableAction<LegalDocumentVersionDto>[];
  textColumns!: TableColumn<LegalDocumentTextSummaryDto>[];
  textActions!: TableAction<LegalDocumentTextSummaryDto>[];

  private readonly destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinitions();
      this.cd.detectChanges();
    });

    this.facade.loadVersions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  audienceLabelKey(audience: LegalDocumentAudience): string {
    return getAudienceLabelKey(audience);
  }

  typeLabelKey(type: LegalDocumentType): string {
    return getTypeLabelKey(type);
  }

  effectiveDate(value: Date | undefined): string {
    return formatEffectiveDate(value);
  }

  private rebuildTableDefinitions(): void {
    const versionDef = getLegalVersionTableDefinition(
      { onPreview: (row) => this.facade.preview(row) },
      this.translate
    );
    this.versionColumns = versionDef.columns;
    this.versionActions = versionDef.actions;

    const textDef = getLegalTextTableDefinition(
      {
        onShow: (row) => this.facade.showLanguage(row),
        isShown: (row) => row.language === this.facade.language(),
      },
      this.translate
    );
    this.textColumns = textDef.columns;
    this.textActions = textDef.actions;
  }
}
