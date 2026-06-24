import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminActionAuditDto,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { AuditLogFacade } from '../audit-log/audit-log.facade';
import {
  getAuditLogTableActions,
  getAuditLogTableColumns,
  getOutcomeClass,
  getOutcomeLabelKey,
} from '../audit-log/audit-log.models';

@Component({
  selector: 'cleansia-admin-audit-resource-history',
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
  templateUrl: './resource-history.component.html',
  providers: [AuditLogFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceHistoryComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(AuditLogFacade);

  readonly outcomeTemplate =
    viewChild<TemplateRef<AdminActionAuditDto>>('outcomeTemplate');

  readonly resourceType = signal<string>('');
  readonly resourceId = signal<string>('');

  auditColumns!: TableColumn<AdminActionAuditDto>[];
  auditActions!: TableAction<AdminActionAuditDto>[];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private readonly destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.cd.detectChanges();
      });

    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe((params) => {
      const resourceType = params.get('resourceType') ?? '';
      const resourceId = params.get('resourceId') ?? '';
      this.resourceType.set(resourceType);
      this.resourceId.set(resourceId);
      if (resourceType && resourceId) {
        this.facade.loadResourceHistory(resourceType, resourceId);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTableDefinitions(): void {
    this.auditColumns = getAuditLogTableColumns(
      this.translate,
      this.outcomeTemplate()
    );
    this.auditActions = getAuditLogTableActions(this.translate, (audit) =>
      this.viewEntry(audit)
    );
  }

  viewEntry(audit: AdminActionAuditDto): void {
    if (!audit.id) return;
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG, 'entry', audit.id]);
  }

  getOutcomeClass(audit: AdminActionAuditDto): string {
    return getOutcomeClass(audit.success);
  }

  getOutcomeLabelKey(audit: AdminActionAuditDto): string {
    return getOutcomeLabelKey(audit.success);
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG]);
  }

  onSortChange(event: { field: string; order: number }): void {
    if (event.field === this.lastSortField && event.order === this.lastSortOrder) {
      return;
    }
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;
    const direction =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    this.facade.onSortChange([
      new SortDefinition({ field: event.field, direction }),
    ]);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }
}
