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
import { Router } from '@angular/router';
import {
  DisputeListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
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
import { DisputesManagementFacade } from './disputes-management.facade';
import {
  DISPUTE_STATUS_LABEL_KEYS,
  getDisputeStatusClass,
  getDisputeTableDefinition,
} from './disputes-management.models';

@Component({
  selector: 'cleansia-admin-disputes-management',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
  ],
  templateUrl: './disputes-management.component.html',
  providers: [DisputesManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DisputesManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(DisputesManagementFacade);

  readonly statusTemplate = viewChild<TemplateRef<DisputeListItem>>('statusTemplate');
  readonly reasonTemplate = viewChild<TemplateRef<DisputeListItem>>('reasonTemplate');

  disputeColumns!: TableColumn<DisputeListItem>[];
  disputeActions!: TableAction<DisputeListItem>[];

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

    this.facade.loadDisputes();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getDisputeTableDefinition(
      { onViewDetails: this.viewDisputeDetails.bind(this) },
      this.translate,
      this.statusTemplate(),
      this.reasonTemplate()
    );
    this.disputeColumns = tableDef.columns;
    this.disputeActions = tableDef.actions;
  }

  viewDisputeDetails(dispute: DisputeListItem): void {
    if (!dispute.id) return;
    this.router.navigate([CleansiaAdminRoute.DISPUTE_MANAGEMENT, dispute.id]);
  }

  getStatusClass(dispute: DisputeListItem): string {
    return getDisputeStatusClass(dispute.status?.value);
  }

  getStatusLabel(dispute: DisputeListItem): string {
    const value = dispute.status?.value;
    if (value == null) return '';
    const key = DISPUTE_STATUS_LABEL_KEYS[value];
    return key ? this.translate.instant(key) : dispute.status?.name ?? '';
  }

  getReasonLabel(dispute: DisputeListItem): string {
    return dispute.reason?.name ?? '';
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
