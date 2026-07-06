import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FiscalErrorKind, FiscalFailureDto } from '@cleansia/admin-services';
import {
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { FiscalFailuresListFacade } from './fiscal-failures-list.facade';
import {
  getFiscalErrorKindBadge,
  getFiscalFailureTableActions,
  getFiscalFailureTableColumns,
} from './fiscal-failures-list.models';

@Component({
  selector: 'cleansia-admin-fiscal-failures-list',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './fiscal-failures-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [FiscalFailuresListFacade],
})
export class FiscalFailuresListComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(FiscalFailuresListFacade);

  errorKindTemplate = viewChild<TemplateRef<FiscalFailureDto>>('errorKindTemplate');

  columns!: TableColumn<FiscalFailureDto>[];
  actions!: TableAction<FiscalFailureDto>[];

  private destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.cd.detectChanges();
      });

    this.facade.loadFailures();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTableDefinitions(): void {
    this.columns = getFiscalFailureTableColumns(
      this.translate,
      this.errorKindTemplate()
    );
    this.actions = getFiscalFailureTableActions(
      {
        onRetry: this.retry.bind(this),
        onAcknowledge: this.acknowledge.bind(this),
      },
      this.translate
    );
  }

  retry(row: FiscalFailureDto): void {
    if (!row.receiptId) return;
    this.facade.retryNow(row.receiptId);
  }

  acknowledge(row: FiscalFailureDto): void {
    if (!row.receiptId) return;
    this.facade.acknowledge(row.receiptId);
  }

  getErrorKindClass(kind: FiscalErrorKind | undefined): string {
    const badge = getFiscalErrorKindBadge(kind);
    return badge
      ? `fiscal-error-badge fiscal-error-${badge}`
      : 'fiscal-error-badge';
  }

  getErrorKindLabel(kind: FiscalErrorKind | undefined): string {
    const badge = getFiscalErrorKindBadge(kind);
    if (!badge) return '-';
    return this.translate.instant(`fiscal_failures.error_kind.${badge}`);
  }
}
