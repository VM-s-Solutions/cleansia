import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminLegalDocumentDto,
  LegalDocumentTextSummaryDto,
  LegalDocumentVersionDto,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { pickPreviewLanguage } from './legal-documents.models';

interface TextRequest {
  documentId: string;
  language: string;
}

@Injectable()
export class LegalDocumentsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly translate = inject(TranslateService);

  readonly versions = signal<LegalDocumentVersionDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);

  readonly selectedVersion = signal<LegalDocumentVersionDto | null>(null);
  readonly language = signal<string | null>(null);
  readonly document = signal<AdminLegalDocumentDto | null>(null);
  readonly documentLoading = signal<boolean>(false);
  readonly documentError = signal<boolean>(false);

  private readonly textRequest$ = new Subject<TextRequest>();

  constructor() {
    super();
    this.textRequest$
      .pipe(
        tap(() => {
          this.document.set(null);
          this.documentError.set(false);
          this.documentLoading.set(true);
        }),
        switchMap(({ documentId, language }) =>
          this.adminClient.adminLegalClient.getDocument(documentId, language).pipe(
            catchError(() => {
              this.documentError.set(true);
              return of(null);
            }),
            finalize(() => this.documentLoading.set(false))
          )
        ),
        takeUntil(this.destroyed$)
      )
      .subscribe((document) => this.document.set(document));
  }

  loadVersions(): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.adminClient.adminLegalClient
      .getVersions(undefined, undefined, undefined)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((versions) => {
        this.versions.set(versions ?? []);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  preview(version: LegalDocumentVersionDto): void {
    this.selectedVersion.set(version);
    this.requestText(pickPreviewLanguage(version.texts, this.translate.currentLang));
  }

  showLanguage(text: LegalDocumentTextSummaryDto): void {
    if (!this.selectedVersion()) return;
    this.requestText(text.language ?? null);
  }

  retryText(): void {
    this.requestText(this.language());
  }

  closePreview(): void {
    this.selectedVersion.set(null);
    this.language.set(null);
    this.document.set(null);
    this.documentError.set(false);
  }

  private requestText(language: string | null): void {
    const documentId = this.selectedVersion()?.id;
    this.language.set(language);
    if (!documentId || !language) {
      this.document.set(null);
      return;
    }
    this.textRequest$.next({ documentId, language });
  }
}
