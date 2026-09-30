import { Injectable, computed, inject, signal } from '@angular/core';
import { AdminCashHeldClient, CleanerCashHeldDto } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import {
  buildRecordCashRemittanceCommand,
  buildWriteOffCashHeldCommand,
  CASH_HELD_ACTION_COPY,
  CashHeldAction,
  CashHeldActionKind,
  parseCashAmount,
} from './cash-held.models';

@Injectable()
export class CashHeldFacade extends UnsubscribeControlDirective {
  private readonly cashHeldClient = inject(AdminCashHeldClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly lang = currentLanguage(this.translate);
  // Not paginated: get-all returns every cleaner holding cash, so there is no totalRecords.
  readonly cashHeld = signal<CleanerCashHeldDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);

  readonly action = signal<CashHeldAction | null>(null);
  readonly amount = signal<string>('');
  readonly note = signal<string>('');
  readonly submitting = signal<boolean>(false);

  readonly actionCopy = computed(() => {
    const action = this.action();
    return action ? CASH_HELD_ACTION_COPY[action.kind] : null;
  });
  readonly canSubmit = computed(() => {
    const copy = this.actionCopy();
    return (
      !!copy &&
      parseCashAmount(this.amount()) !== null &&
      (!copy.noteRequired || this.note().trim().length > 0) &&
      !this.submitting()
    );
  });

  loadCashHeld(): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.cashHeldClient
      .getAll()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.cashHeld.set(response);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  startRemittance(row: CleanerCashHeldDto): void {
    this.start(CashHeldActionKind.Remittance, row);
  }

  startWriteOff(row: CleanerCashHeldDto): void {
    this.start(CashHeldActionKind.WriteOff, row);
  }

  setAmount(value: string): void {
    this.amount.set(value);
  }

  setNote(value: string): void {
    this.note.set(value);
  }

  cancelAction(): void {
    this.action.set(null);
    this.amount.set('');
    this.note.set('');
  }

  submit(): void {
    const action = this.action();
    const amount = parseCashAmount(this.amount());
    const note = this.note().trim();
    if (!action || amount === null || !this.canSubmit()) return;

    const request$: Observable<unknown> =
      action.kind === CashHeldActionKind.Remittance
        ? this.cashHeldClient.recordRemittance(buildRecordCashRemittanceCommand(action.row, amount, note))
        : this.cashHeldClient.writeOff(buildWriteOffCashHeldCommand(action.row, amount, note));
    const successKey = CASH_HELD_ACTION_COPY[action.kind].successKey;

    this.submitting.set(true);
    request$
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(successKey);
          this.cancelAction();
        }
        this.loadCashHeld();
      });
  }

  private start(kind: CashHeldActionKind, row: CleanerCashHeldDto): void {
    if (!row.employeeId || !row.currencyId || this.submitting()) return;
    this.action.set({ kind, row });
    this.amount.set(String(row.amount));
    this.note.set('');
  }
}
