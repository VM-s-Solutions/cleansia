import { computed, inject, Injectable, signal } from '@angular/core';
import { AdminClient, WorkContractDto } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { PermissionService, Policy } from '@cleansia/services';
import { buildWorkContractFactRows, languageDisplayName } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, finalize, map, Observable, of, switchMap, takeUntil } from 'rxjs';
import {
  buildWorkContractAcceptanceRows,
  WORK_CONTRACT_FACTS_KEY,
} from './admin-work-contract-dialog.models';

interface ContractRead {
  contract: WorkContractDto;
  acceptedTextHash: string | null;
}

@Injectable()
export class AdminWorkContractDialogFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly permissions = inject(PermissionService);
  private readonly translate = inject(TranslateService);
  private readonly dialogRef = inject(DynamicDialogRef);

  readonly contract = signal<WorkContractDto | null>(null);
  readonly acceptedTextHash = signal<string | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly language = signal<string>(this.translate.currentLang);

  // The document read behind the hash is Administrator-only, while the order detail is open to
  // every admin role; a read the session cannot make is never attempted.
  private readonly hashReadable = this.permissions.hasPolicy(Policy.CanViewLegalDocuments);

  readonly factRows = computed(() =>
    buildWorkContractFactRows(this.contract()?.facts, this.language(), WORK_CONTRACT_FACTS_KEY)
  );

  readonly acceptanceRows = computed(() =>
    buildWorkContractAcceptanceRows(
      this.contract(),
      this.acceptedTextHash(),
      this.language(),
      this.hashReadable
    )
  );

  readonly renderedLanguageNotice = computed(() => {
    const contract = this.contract();
    const rendered = contract?.language;
    const accepted = contract?.acceptance?.acceptedLanguage;
    if (!rendered || !accepted || rendered === accepted) return null;
    return {
      language: languageDisplayName(rendered, this.language()),
      accepted: languageDisplayName(accepted, this.language()),
    };
  });

  private acceptanceId: string | null = null;

  load(acceptanceId: string): void {
    this.acceptanceId = acceptanceId;
    this.fetch();
  }

  retry(): void {
    this.fetch();
  }

  close(): void {
    this.dialogRef.close();
  }

  private fetch(): void {
    const acceptanceId = this.acceptanceId;
    if (!acceptanceId) return;

    const lang = this.translate.currentLang;
    this.language.set(lang);
    this.loading.set(true);
    this.loadFailed.set(false);
    this.contract.set(null);
    this.acceptedTextHash.set(null);

    this.adminClient.adminOrderClient
      .getWorkContract(acceptanceId, lang)
      .pipe(
        switchMap((contract) =>
          this.readAcceptedTextHash(contract).pipe(
            map((acceptedTextHash): ContractRead => ({ contract, acceptedTextHash }))
          )
        ),
        takeUntil(this.destroyed$),
        catchError(() => {
          this.loadFailed.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((read) => {
        if (read) {
          this.contract.set(read.contract);
          this.acceptedTextHash.set(read.acceptedTextHash);
        }
      });
  }

  // The hash a dispute cites is the admin document read's, for the row the cleaner accepted — the
  // dialog may render another language. That read falls back to another language when the asked-for
  // one is missing, so a hash is only shown when the answer is the accepted language.
  private readAcceptedTextHash(contract: WorkContractDto): Observable<string | null> {
    const documentId = contract.legalDocumentId;
    const acceptedLanguage = contract.acceptance?.acceptedLanguage;
    if (!this.hashReadable || !documentId || !acceptedLanguage) return of(null);

    return this.adminClient.adminLegalClient.getDocument(documentId, acceptedLanguage).pipe(
      map((document) =>
        document.language === acceptedLanguage ? document.contentHash ?? null : null
      ),
      catchError(() => of(null))
    );
  }
}
