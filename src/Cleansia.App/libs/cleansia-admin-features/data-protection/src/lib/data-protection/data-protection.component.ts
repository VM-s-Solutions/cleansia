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
  FormBuilder,
  FormControl,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  GdprRequestDto,
  GdprRequestStatus,
  UserConsentDto,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { PermissionService, Policy } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { DataProtectionFacade } from './data-protection.facade';
import {
  getConsentTableDefinition,
  getGdprRequestStatusOptions,
  getGdprRequestTableDefinition,
} from './data-protection.models';

@Component({
  selector: 'cleansia-admin-data-protection',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './data-protection.component.html',
  providers: [DataProtectionFacade, ConfirmationService],
})
export class DataProtectionComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly cd = inject(ChangeDetectorRef);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly permissions = inject(PermissionService);
  protected readonly facade = inject(DataProtectionFacade);
  protected readonly Policy = Policy;

  requestColumns!: TableColumn<GdprRequestDto>[];
  requestActions!: TableAction<GdprRequestDto>[];
  consentColumns!: TableColumn<UserConsentDto>[];
  statusOptions: ICleansiaSelectOption[] = [];

  private readonly destroy$ = new Subject<void>();

  readonly statusControl = new FormControl<GdprRequestStatus | null>(null);

  readonly userForm = this.fb.nonNullable.group({
    userId: this.fb.nonNullable.control<string>('', [Validators.required]),
  });

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinitions();
      this.cd.detectChanges();
    });

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((status) => this.facade.selectStatus(status));

    this.facade.loadRequests();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  onRequestsPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  loadConsents(): void {
    const userId = this.requireUserId();
    if (userId) {
      this.facade.loadConsents(userId);
    }
  }

  exportData(): void {
    const userId = this.requireUserId();
    if (userId) {
      this.facade.exportUserData(userId);
    }
  }

  confirmErase(): void {
    const userId = this.requireUserId();
    if (!userId) return;

    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.data_protection.erase.confirm_message',
        { userId }
      ),
      header: this.translate.instant(
        'pages.data_protection.erase.confirm_title'
      ),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant(
        'pages.data_protection.erase.confirm_yes'
      ),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => this.facade.eraseUserAccount(userId),
    });
  }

  /**
   * Fulfil a filed deletion request — the admin half of ADR-0052. Reuses the same erase command the
   * form below calls; the only thing this adds is that the user id comes from the row that asked,
   * so an admin never has to copy one across.
   */
  confirmFulfil(row: GdprRequestDto): void {
    // userId is optional on the generated DTO. A request row without one cannot be fulfilled and
    // the action is hidden for it, but the guard stays: the caller is a template binding, not a
    // type the compiler can narrow for us.
    const userId = row.userId;
    if (!userId) return;

    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.data_protection.erase.confirm_message',
        { userId }
      ),
      header: this.translate.instant(
        'pages.data_protection.requests.fulfil_title'
      ),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant(
        'pages.data_protection.erase.confirm_yes'
      ),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => this.facade.eraseUserAccount(userId),
    });
  }

  confirmRetry(row: GdprRequestDto): void {
    const requestId = row.id;
    if (!requestId) return;

    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.data_protection.requests.retry_confirm_message',
        { userId: row.userId ?? '—' }
      ),
      header: this.translate.instant(
        'pages.data_protection.requests.retry_confirm_title'
      ),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant(
        'pages.data_protection.requests.retry_confirm_yes'
      ),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => this.facade.retryDeletion(requestId),
    });
  }

  formatDate(d?: Date): string {
    return formatDate(d, this.translate.currentLang, 'dateTime') || '—';
  }

  private requireUserId(): string | null {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return null;
    }
    return this.userForm.getRawValue().userId.trim() || null;
  }

  private rebuildTableDefinitions(): void {
    const requestTable = getGdprRequestTableDefinition(
      {
        onFulfil: (row) => this.confirmFulfil(row),
        onRetry: (row) => this.confirmRetry(row),
        retrying: () => this.facade.retrying(),
      },
      this.translate,
      this.permissions,
      (d) => this.formatDate(d)
    );
    this.requestColumns = requestTable.columns;
    this.requestActions = requestTable.actions;
    this.statusOptions = getGdprRequestStatusOptions(this.translate);
    this.consentColumns = getConsentTableDefinition(this.translate, (d) =>
      this.formatDate(d)
    ).columns;
  }
}
