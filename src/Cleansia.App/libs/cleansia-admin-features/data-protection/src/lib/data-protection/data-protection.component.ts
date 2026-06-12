import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { GdprRequestDto, UserConsentDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { DataProtectionFacade } from './data-protection.facade';
import {
  getConsentTableDefinition,
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
  protected readonly facade = inject(DataProtectionFacade);
  protected readonly Policy = Policy;

  requestColumns!: TableColumn<GdprRequestDto>[];
  consentColumns!: TableColumn<UserConsentDto>[];

  private readonly destroy$ = new Subject<void>();

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

  formatDate(d?: Date): string {
    if (!d) return '—';
    return new Intl.DateTimeFormat(this.translate.currentLang ?? 'en', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(d);
  }

  private requireUserId(): string | null {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return null;
    }
    return this.userForm.getRawValue().userId.trim() || null;
  }

  private rebuildTableDefinitions(): void {
    this.requestColumns = getGdprRequestTableDefinition(
      this.translate,
      (d) => this.formatDate(d)
    ).columns;
    this.consentColumns = getConsentTableDefinition(this.translate, (d) =>
      this.formatDate(d)
    ).columns;
  }
}
