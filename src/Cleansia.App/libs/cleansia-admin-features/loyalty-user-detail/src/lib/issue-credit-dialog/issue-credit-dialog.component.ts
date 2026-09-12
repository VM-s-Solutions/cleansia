import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaSelectComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { CreditTransactionReason } from '@cleansia/admin-services';
import { ICleansiaSelectOption } from '@cleansia/components';
import type { CreditCurrencyOption } from '../user-loyalty-detail/user-loyalty-detail.facade';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';

export interface IssueCreditDialogSubmit {
  amount: number;
  currencyId: string;
  reason: CreditTransactionReason;
  note: string;
}

/** Mirrors IssueCustomerCredit.Validator.SanityCap — a typo guard, not a business rule. */
const AMOUNT_MAX = 10000;
const NOTE_MAX = 500;

/**
 * Putting money on a customer's balance.
 *
 * <p>Deliberately a separate dialog from the points one, not a mode of it. Points are a score and a
 * mis-keyed grant is embarrassing; credit is a debt the company takes on, and the two forms need
 * different fields, different validation and — most of all — different weight on the screen.</p>
 *
 * <p>The reason list is the server's issuable set, not the whole enum: <c>OrderPayment</c> describes
 * money LEAVING a balance and the server refuses it, so offering it here would only produce a 400 the
 * admin cannot act on.</p>
 */
@Component({
  selector: 'cleansia-admin-issue-credit-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaSelectComponent,
    CleansiaTextareaComponent,
    CleansiaTextInputComponent,
  ],
  templateUrl: './issue-credit-dialog.component.html',
})
export class IssueCreditDialogComponent {
  private readonly fb = inject(FormBuilder);

  /** Two-way bound visibility flag — wire via [(visible)]. */
  readonly visible = input<boolean>(false);
  readonly visibleChange = output<boolean>();

  readonly submitting = input<boolean>(false);
  /**
   * The currencies on offer — the platform's ACTIVE ones, loaded by the facade. Deliberately NOT
   * preselected: the defect this closes was a grant whose label said one currency while the server
   * wrote another, and a pre-filled value is exactly the thing that goes unread on a money-out form.
   */
  readonly currencies = input<CreditCurrencyOption[]>([]);

  readonly currencyOptions = computed<ICleansiaSelectOption[]>(() =>
    this.currencies().map((c) => ({ label: c.code, value: c.id })),
  );

  readonly submitForm = output<IssueCreditDialogSubmit>();

  private readonly translate = inject(TranslateService);

  /**
   * The server's ISSUABLE set, not the whole enum. `OrderPayment` describes money LEAVING a balance
   * and the validator refuses it, so offering it would only produce a 400 the admin cannot act on;
   * `OrderPaymentReturned` is accepted by the server but is the automatic return path's own reason,
   * and an admin choosing it by hand almost always meant Goodwill.
   */
  readonly reasonOptions = computed<ICleansiaSelectOption[]>(() => [
    {
      value: CreditTransactionReason.DisputeSettlement,
      label: this.translate.instant(
        'pages.loyalty_user_detail.credit.reason.dispute_settlement',
      ),
    },
    {
      value: CreditTransactionReason.CleanerNoShow,
      label: this.translate.instant(
        'pages.loyalty_user_detail.credit.reason.cleaner_no_show',
      ),
    },
    {
      value: CreditTransactionReason.Goodwill,
      label: this.translate.instant('pages.loyalty_user_detail.credit.reason.goodwill'),
    },
  ]);

  readonly form = this.fb.group({
    currencyId: this.fb.control<string | null>(null, {
      validators: [Validators.required],
    }),
    amount: this.fb.control<number | null>(null, {
      validators: [
        Validators.required,
        // The server refuses zero and anything finer than a minor unit; both are caught here so the
        // admin is told before a round trip.
        Validators.min(0.01),
        Validators.max(AMOUNT_MAX),
      ],
    }),
    reason: this.fb.control<CreditTransactionReason>(
      CreditTransactionReason.Goodwill,
      { nonNullable: true, validators: [Validators.required] },
    ),
    note: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(NOTE_MAX)],
    }),
  });

  reset(): void {
    this.form.reset({
      currencyId: null,
      amount: null,
      reason: CreditTransactionReason.Goodwill,
      note: '',
    });
  }

  onVisibilityChanged(value: boolean): void {
    if (!value) {
      this.reset();
    }
    this.visibleChange.emit(value);
  }

  cancel(): void {
    this.onVisibilityChanged(false);
  }

  submit(): void {
    const v = this.form.getRawValue();
    if (this.form.invalid || !v.currencyId) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitForm.emit({
      // Rounded here as well as validated: a browser number input will hand back 10.005 quite
      // happily, and the server refuses anything finer than a minor unit.
      amount: Math.round(Number(v.amount) * 100) / 100,
      currencyId: v.currencyId,
      reason: v.reason,
      note: v.note.trim(),
    });
  }
}
