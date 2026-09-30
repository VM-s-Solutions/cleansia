import { Injectable, computed, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AcceptLegalDocumentCommand,
  CleanerLegalDocumentDto,
  LegalDocumentType,
  PartnerClient,
} from '@cleansia/partner-services';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { finalize, takeUntil } from 'rxjs';

const DOCUMENT_NOT_IN_FORCE = 'legal.document_not_in_force';

@Injectable()
export class ProfileLegalDocumentsFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  readonly documents = signal<CleanerLegalDocumentDto[]>([]);
  readonly loading = signal(false);
  readonly loaded = signal(false);
  readonly loadFailed = signal(false);
  readonly acceptingType = signal<LegalDocumentType | null>(null);

  readonly accepting = computed(() => this.acceptingType() !== null);
  readonly visible = computed(() => !this.loaded() || this.documents().length > 0);

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    this.partnerClient.employeeClient
      .getMyLegalDocuments(this.translate.currentLang)
      .pipe(
        takeUntil(this.destroyed$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (documents) => {
          this.documents.set(documents ?? []);
          this.loaded.set(true);
        },
        error: () => this.loadFailed.set(!this.loaded()),
      });
  }

  retry(): void {
    this.load();
  }

  accept(document: CleanerLegalDocumentDto): void {
    const textId = document.legalDocumentTextId;
    if (!textId || this.accepting()) {
      return;
    }

    this.acceptingType.set(document.type);

    const command = new AcceptLegalDocumentCommand();
    command.acceptedTextId = textId;

    this.partnerClient.employeeClient
      .acceptLegalDocument(command)
      .pipe(
        takeUntil(this.destroyed$),
        finalize(() => this.acceptingType.set(null))
      )
      .subscribe({
        next: () => {
          this.snackbarService.showSuccessTranslated(
            'global.messages.profile.legal_document_accepted'
          );
          this.load();
        },
        error: (error: unknown) => {
          if (extractApiErrorCode(error) === DOCUMENT_NOT_IN_FORCE) {
            this.load();
          }
        },
      });
  }
}
