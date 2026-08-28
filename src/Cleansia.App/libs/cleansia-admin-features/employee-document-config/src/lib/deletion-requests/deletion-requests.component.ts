import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import {
  DocumentDeletionRequestDto,
  DocumentDeletionRequestStatus,
  DocumentType,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  TableAction,
  TableColumn,
  TableConfig,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DOCUMENT_TYPE_LABEL_KEYS } from '../document-type-labels';
import { DeletionRequestsFacade } from './deletion-requests.facade';

@Component({
  selector: 'cleansia-admin-deletion-requests',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTextareaComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './deletion-requests.component.html',
  providers: [DeletionRequestsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeletionRequestsComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(DeletionRequestsFacade);
  protected readonly Policy = Policy;

  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  /** The request being answered. An inline panel, not a modal — the shared set has no dialog. */
  protected readonly reviewing = signal<DocumentDeletionRequestDto | null>(null);
  protected readonly notesControl = new FormControl<string>('', { nonNullable: true });

  protected readonly statusControl = new FormControl<DocumentDeletionRequestStatus | null>(
    DocumentDeletionRequestStatus.Pending,
  );

  protected readonly statusOptions = computed<ICleansiaSelectOption[]>(() => [
    {
      label: this.translate.instant('pages.document_deletion_requests.status.pending'),
      value: DocumentDeletionRequestStatus.Pending,
    },
    {
      label: this.translate.instant('pages.document_deletion_requests.status.approved'),
      value: DocumentDeletionRequestStatus.Approved,
    },
    {
      label: this.translate.instant('pages.document_deletion_requests.status.rejected'),
      value: DocumentDeletionRequestStatus.Rejected,
    },
  ]);

  protected readonly columns: TableColumn<DocumentDeletionRequestDto>[] = [
    {
      id: 'employeeName',
      field: 'employeeName',
      header: 'pages.document_deletion_requests.columns.employee',
    },
    {
      id: 'documentType',
      field: 'documentType',
      header: 'pages.document_deletion_requests.columns.document_type',
      getValue: (row) => this.translate.instant(this.documentTypeLabelKey(row.documentType)),
    },
    {
      id: 'documentFileName',
      field: 'documentFileName',
      header: 'pages.document_deletion_requests.columns.file',
    },
    {
      id: 'reason',
      field: 'reason',
      header: 'pages.document_deletion_requests.columns.reason',
    },
    {
      id: 'createdOn',
      field: 'createdOn',
      header: 'pages.document_deletion_requests.columns.requested_on',
    },
    {
      id: 'status',
      field: 'status',
      header: 'pages.document_deletion_requests.columns.status',
      align: 'center',
      getValue: (row) => this.translate.instant(this.statusLabelKey(row.status)),
    },
  ];

  protected readonly actions: TableAction<DocumentDeletionRequestDto>[] = [
    {
      icon: 'pi pi-check-square',
      tooltip: 'pages.document_deletion_requests.review',
      // Only a pending request can be answered; the others are here as a record.
      visible: (row) => row.status === DocumentDeletionRequestStatus.Pending,
      onClick: (row) => this.startReview(row),
    },
  ];

  /** No paginator: `deletionRequests` returns one array for the chosen status. */
  protected readonly tableConfig: TableConfig = {
    paginator: false,
    hover: true,
    emptyMessage: 'pages.document_deletion_requests.empty',
  };

  ngOnInit(): void {
    this.facade.load();
    this.statusControl.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((status) => {
        this.reviewing.set(null);
        this.facade.selectStatus(status ?? null);
      });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  protected startReview(request: DocumentDeletionRequestDto): void {
    this.notesControl.setValue('');
    this.reviewing.set(request);
  }

  protected cancelReview(): void {
    this.reviewing.set(null);
  }

  /**
   * Approving is what actually deletes the document — the request never touched it. Rejecting
   * leaves the document in place and records why, which is what the cleaner reads.
   */
  protected resolve(approve: boolean): void {
    const request = this.reviewing();
    if (!request?.id) {
      return;
    }
    const notes = this.notesControl.value.trim();
    this.facade.resolve(request.id, approve, notes.length > 0 ? notes : null);
    this.reviewing.set(null);
  }

  protected documentTypeLabelKey(type: DocumentType | null | undefined): string {
    return type == null
      ? 'pages.employee_detail.document_types.unknown'
      : (DOCUMENT_TYPE_LABEL_KEYS[type] ?? 'pages.employee_detail.document_types.unknown');
  }

  protected statusLabelKey(status: DocumentDeletionRequestStatus | null | undefined): string {
    switch (status) {
      case DocumentDeletionRequestStatus.Pending:
        return 'pages.document_deletion_requests.status.pending';
      case DocumentDeletionRequestStatus.Approved:
        return 'pages.document_deletion_requests.status.approved';
      case DocumentDeletionRequestStatus.Rejected:
        return 'pages.document_deletion_requests.status.rejected';
      default:
        return 'pages.document_deletion_requests.status.unknown';
    }
  }
}
