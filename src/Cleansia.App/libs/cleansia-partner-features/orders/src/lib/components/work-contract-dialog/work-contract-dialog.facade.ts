import { computed, inject, Injectable, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { PartnerClient, WorkContractDto } from '@cleansia/partner-services';
import { extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, finalize, Observable, of, takeUntil } from 'rxjs';
import {
  buildAcceptWorkContractCommand,
  buildTakeOrderCommand,
  buildWorkContractFactRows,
  languageDisplayName,
  WorkContractDialogData,
  WorkContractDialogMode,
  WorkContractDialogOutcome,
  WorkContractDialogResult,
} from './work-contract-dialog.models';

const TEXT_MISMATCH = 'contract.text_mismatch';
const DOCUMENT_NOT_FOUND = 'legal.document_not_found';

@Injectable()
export class WorkContractDialogFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private readonly dialogRef = inject(DynamicDialogRef);

  readonly contract = signal<WorkContractDto | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly submitting = signal(false);
  readonly accepted = signal(false);
  readonly noticeKey = signal<string | null>(null);
  readonly language = signal<string>(this.translate.currentLang);
  readonly mode = signal<WorkContractDialogMode>(WorkContractDialogMode.Take);

  readonly factRows = computed(() =>
    buildWorkContractFactRows(this.contract()?.facts, this.language())
  );

  readonly acceptedLanguageName = computed(() => {
    const contract = this.contract();
    const acceptedLanguage = contract?.acceptance?.acceptedLanguage;
    if (!acceptedLanguage || acceptedLanguage === contract?.language) return null;
    return languageDisplayName(acceptedLanguage, this.language());
  });

  readonly canSubmit = computed(
    () =>
      this.mode() !== WorkContractDialogMode.Read &&
      this.accepted() &&
      !!this.contract()?.legalDocumentTextId &&
      !this.loading() &&
      !this.submitting()
  );

  private data: WorkContractDialogData | null = null;
  private acceptanceControl: FormControl<boolean> | null = null;

  load(data: WorkContractDialogData): void {
    this.data = data;
    this.mode.set(data.mode);
    this.fetch();
  }

  retry(): void {
    this.fetch();
  }

  connectAcceptanceControl(control: FormControl<boolean>): void {
    this.acceptanceControl = control;
    control.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe((value) => this.accepted.set(value));
  }

  submit(): void {
    const data = this.data;
    const textId = this.contract()?.legalDocumentTextId;
    if (!data || data.mode === WorkContractDialogMode.Read || !textId || !this.canSubmit()) {
      return;
    }

    this.submitting.set(true);
    this.noticeKey.set(null);

    const request$: Observable<unknown> =
      data.mode === WorkContractDialogMode.Take
        ? this.partnerClient.orderClient.takeOrder(buildTakeOrderCommand(data.orderId, textId))
        : this.partnerClient.orderClient.acceptWorkContract(
            buildAcceptWorkContractCommand(data.orderId, textId)
          );

    request$
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error) => {
          this.handleRefusal(error);
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.close({ outcome: WorkContractDialogOutcome.Accepted });
        }
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  private fetch(): void {
    const data = this.data;
    if (!data) return;

    const lang = this.translate.currentLang;
    this.language.set(lang);
    this.loading.set(true);
    this.loadFailed.set(false);
    this.contract.set(null);

    const request$ =
      data.mode === WorkContractDialogMode.Read
        ? this.partnerClient.orderClient.getWorkContract(data.acceptanceId, lang)
        : this.partnerClient.orderClient.getWorkContractPreview(data.orderId, lang);

    request$
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error) => {
          this.handleLoadFailure(error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((contract) => {
        if (contract) {
          this.contract.set(contract);
        }
      });
  }

  private handleLoadFailure(error: unknown): void {
    if (extractApiErrorCode(error) === DOCUMENT_NOT_FOUND) {
      this.noticeKey.set(`api.${DOCUMENT_NOT_FOUND}`);
      return;
    }
    this.loadFailed.set(true);
  }

  // The acceptance echoes the text row it rendered; a mismatch means the text moved under the
  // reader, so the tick they gave no longer refers to what the server would record.
  private handleRefusal(error: unknown): void {
    if (extractApiErrorCode(error) === TEXT_MISMATCH) {
      this.untick();
      this.noticeKey.set(`api.${TEXT_MISMATCH}`);
      this.fetch();
      return;
    }
    this.close({ outcome: WorkContractDialogOutcome.Refused });
  }

  private untick(): void {
    this.accepted.set(false);
    this.acceptanceControl?.setValue(false);
  }

  private close(result: WorkContractDialogResult): void {
    this.dialogRef.close(result);
  }
}
