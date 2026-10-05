import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { ReferralInterventionDialogComponent } from './referral-intervention-dialog.component';
import { ReferralInterventionMode, ReferralInterventionSubmit } from './referral-intervention-dialog.models';

describe('ReferralInterventionDialogComponent', () => {
  let fixture: ComponentFixture<ReferralInterventionDialogComponent>;
  let component: ReferralInterventionDialogComponent;

  async function mount(mode: ReferralInterventionMode, credit: string | null = null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ReferralInterventionDialogComponent, TranslateModule.forRoot()],
    })
      .overrideComponent(ReferralInterventionDialogComponent, { set: { template: '', imports: [] } })
      .compileComponents();

    fixture = TestBed.createComponent(ReferralInterventionDialogComponent);
    fixture.componentRef.setInput('mode', mode);
    fixture.componentRef.setInput('credit', credit);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const copyOf = () => ({
    header: component.headerKey(),
    hint: component.hintKey(),
    submit: component.submitKey(),
    destructive: component.destructive(),
  });

  it('asks to release a held referral, which pays it and is not destructive', async () => {
    await mount('release');

    expect(copyOf()).toEqual({
      header: 'pages.loyalty_referrals.intervention.title_release',
      hint: 'pages.loyalty_referrals.intervention.hint_release',
      submit: 'pages.loyalty_referrals.intervention.submit_release',
      destructive: false,
    });
  });

  it('asks to reject a held referral on the destructive outline', async () => {
    await mount('reject');

    expect(copyOf()).toEqual({
      header: 'pages.loyalty_referrals.intervention.title_reject',
      hint: 'pages.loyalty_referrals.intervention.hint_reject',
      submit: 'pages.loyalty_referrals.intervention.submit_reject',
      destructive: true,
    });
  });

  it('keeps reverse destructive and force-qualify not', async () => {
    await mount('reverse');
    expect(copyOf()).toEqual({
      header: 'pages.loyalty_referrals.intervention.title_reverse',
      hint: 'pages.loyalty_referrals.intervention.hint_reverse',
      submit: 'pages.loyalty_referrals.intervention.submit_reverse',
      destructive: true,
    });

    fixture.componentRef.setInput('mode', 'forceQualify');
    expect(copyOf()).toEqual({
      header: 'pages.loyalty_referrals.intervention.title_force_qualify',
      hint: 'pages.loyalty_referrals.intervention.hint_force_qualify',
      submit: 'pages.loyalty_referrals.intervention.submit_force_qualify',
      destructive: false,
    });
  });

  it('shows what was granted only when reversing, never on a rejection that paid nothing', async () => {
    await mount('reverse', '150,00 Kč / 150,00 Kč');
    expect(component.reverseSummary()).toBe('150,00 Kč / 150,00 Kč');

    fixture.componentRef.setInput('mode', 'reject');
    expect(component.reverseSummary()).toBeNull();
  });

  it('submits a rejection with its mode and the trimmed reason', async () => {
    await mount('reject');
    const emitted: ReferralInterventionSubmit[] = [];
    component.submitForm.subscribe((payload) => emitted.push(payload));

    component.form.controls.reason.setValue('  one household  ');
    component.submit();

    expect(emitted).toEqual([{ mode: 'reject', reason: 'one household' }]);
  });

  it('submits nothing without a reason', async () => {
    await mount('release');
    const emitted: ReferralInterventionSubmit[] = [];
    component.submitForm.subscribe((payload) => emitted.push(payload));

    component.submit();

    expect(emitted).toEqual([]);
  });
});
