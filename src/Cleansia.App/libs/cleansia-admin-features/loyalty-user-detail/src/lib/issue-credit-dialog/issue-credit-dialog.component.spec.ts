import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { CreditTransactionReason } from '@cleansia/admin-services';
import { TranslateModule } from '@ngx-translate/core';
import { IssueCreditDialogComponent } from './issue-credit-dialog.component';

describe('IssueCreditDialogComponent', () => {
  function offeredReasons(): CreditTransactionReason[] {
    const fixture = TestBed.createComponent(IssueCreditDialogComponent);
    return fixture.componentInstance.reasonOptions().map((option) => option.value);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [IssueCreditDialogComponent, TranslateModule.forRoot()],
      providers: [provideNoopAnimations()],
    });
  });

  it('does not offer a dispute settlement, which only resolving the dispute writes', () => {
    expect(offeredReasons()).not.toContain(CreditTransactionReason.DisputeSettlement);
  });

  it('names the amount in the currency the admin picks, and stops naming one when the form resets', () => {
    const fixture = TestBed.createComponent(IssueCreditDialogComponent);
    fixture.componentRef.setInput('currencies', [
      { id: 'cur-czk', code: 'CZK' },
      { id: 'cur-eur', code: 'EUR' },
    ]);
    fixture.detectChanges();
    const dialog = fixture.componentInstance;

    expect(dialog.amountLabel()).toEqual({
      key: 'pages.loyalty_user_detail.credit.dialog.field.amount',
      params: {},
    });

    dialog.form.controls.currencyId.setValue('cur-eur');
    expect(dialog.amountLabel()).toEqual({
      key: 'pages.loyalty_user_detail.credit.dialog.field.amount_in',
      params: { currency: 'EUR' },
    });

    dialog.reset();
    expect(dialog.amountLabel().key).toBe('pages.loyalty_user_detail.credit.dialog.field.amount');
  });

  it('offers a cleaner no-show and goodwill', () => {
    expect(offeredReasons()).toEqual([
      CreditTransactionReason.CleanerNoShow,
      CreditTransactionReason.Goodwill,
    ]);
  });
});
