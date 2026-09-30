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

  it('offers a cleaner no-show and goodwill', () => {
    expect(offeredReasons()).toEqual([
      CreditTransactionReason.CleanerNoShow,
      CreditTransactionReason.Goodwill,
    ]);
  });
});
