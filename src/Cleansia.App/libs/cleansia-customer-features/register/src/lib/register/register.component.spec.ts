import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { selectCustomerLoading } from '@cleansia/customer-stores';
import { GOOGLE_CLIENT_ID } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { RegisterComponent } from './register.component';
import { RegisterFacade, ReferralUiState } from './register.facade';

/**
 * The sign-up referral dialog states what the new customer is credited in the browsed market, as the facade
 * formatted it (`referralCreditAmount`), and a market that pays none reads the `_no_amount` lines.
 * `register.models.spec.ts` pins the key choice; this pins that the page hands the helpers the
 * facade's figure rather than one of its own. -> /product/business-rules#money-constants
 */
describe('RegisterComponent — what the referral dialog promises', () => {
  // A real dictionary: with no loader ngx-translate echoes the bare key and drops the params, so an
  // assertion on the rendered amount would pass on any figure at all.
  const DICTIONARY = {
    auth: {
      register: {
        referral: {
          row_title: 'Referral code',
          dialog_title: 'Enter referral code',
          dialog_helper: 'You get {{amount}} in credit.',
          dialog_helper_no_amount: 'Enter the code here.',
          dialog_success: 'Code accepted — you get {{amount}}.',
          dialog_success_no_amount: 'Code accepted.',
          dialog_success_named: 'Code from {{name}} accepted — you get {{amount}}.',
          dialog_success_named_no_amount: 'Code from {{name}} accepted.',
        },
      },
    },
  };

  async function renderDialog(amount: string | null, state: ReferralUiState): Promise<{ helper: string; status: string }> {
    const facade = {
      formGroup: new FormGroup({
        firstName: new FormControl(''),
        lastName: new FormControl(''),
        email: new FormControl(''),
        password: new FormControl(''),
        confirmPassword: new FormControl(''),
        referralCode: new FormControl(''),
        terms: new FormControl(false),
      }),
      referralCode: signal(state.kind === 'valid' ? 'FRIEND1' : ''),
      referralState: signal<ReferralUiState>(state),
      referralCreditAmount: signal<string | null>(amount),
      termsAccepted: signal(false),
      applyReferralCodeFromUrl: jest.fn(),
      validateReferralCodeNow: jest.fn(),
      clearReferralCode: jest.fn(),
      register: jest.fn(),
      socialSignUpBlocked: jest.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [
        RegisterComponent,
        TranslateModule.forRoot({
          loader: { provide: TranslateLoader, useValue: { getTranslation: () => of(DICTIONARY) } },
        }),
      ],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideMockStore({ selectors: [{ selector: selectCustomerLoading, value: false }] }),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({}) } } },
      ],
    })
      .overrideComponent(RegisterComponent, {
        set: { providers: [{ provide: RegisterFacade, useValue: facade }] },
      })
      .compileComponents();

    TestBed.inject(TranslateService).use('en');
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.cl-auth__code-row')?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (selector: string) =>
      (document.body.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
    return {
      helper: text('.cleansia-code-input-dialog__helper'),
      status: text('.cleansia-code-input-dialog__status--success'),
    };
  }

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('states the facade credit in the helper line', async () => {
    const { helper } = await renderDialog('6,00 €', { kind: 'idle' });

    expect(helper).toBe('You get 6,00 € in credit.');
  });

  it('states the facade credit when the code is accepted, naming the referrer', async () => {
    const { helper, status } = await renderDialog('6,00 €', { kind: 'valid', referrerFirstName: 'Petra' });

    expect(helper).toBe('You get 6,00 € in credit.');
    expect(status).toBe('Code from Petra accepted — you get 6,00 €.');
  });

  it('states the facade credit when the code is accepted without a referrer name', async () => {
    const { status } = await renderDialog('6,00 €', { kind: 'valid', referrerFirstName: null });

    expect(status).toBe('Code accepted — you get 6,00 €.');
  });

  it('reads the no-amount lines in a market that pays none', async () => {
    const named = await renderDialog(null, { kind: 'valid', referrerFirstName: 'Petra' });

    expect(named.helper).toBe('Enter the code here.');
    expect(named.status).toBe('Code from Petra accepted.');
  });

  it('confirms an unnamed code without an amount in a market that pays none', async () => {
    const { helper, status } = await renderDialog(null, { kind: 'valid', referrerFirstName: null });

    expect(helper).toBe('Enter the code here.');
    expect(status).toBe('Code accepted.');
  });
});

type GoogleWindow = Window & { google?: unknown };

/**
 * Google draws its button in the language it is given, else in the browser's or the Google account's,
 * which put a Czech button on an English page.
 */
describe('RegisterComponent — the Google button', () => {
  let renderButton: jest.Mock;

  beforeEach(() => {
    renderButton = jest.fn((parent: HTMLElement) => parent.appendChild(document.createElement('iframe')));
    (window as GoogleWindow).google = { accounts: { id: { initialize: jest.fn(), renderButton } } };
  });

  afterEach(() => {
    delete (window as GoogleWindow).google;
    document.head.querySelectorAll('script[src*="accounts.google.com"]').forEach((script) => script.remove());
  });

  function render(lang: string) {
    const facade = {
      formGroup: new FormGroup({
        firstName: new FormControl(''),
        lastName: new FormControl(''),
        email: new FormControl(''),
        password: new FormControl(''),
        confirmPassword: new FormControl(''),
        referralCode: new FormControl(''),
        terms: new FormControl(false),
      }),
      referralCode: signal(''),
      referralState: signal<ReferralUiState>({ kind: 'idle' }),
      referralCreditAmount: signal<string | null>(null),
      termsAccepted: signal(false),
      applyReferralCodeFromUrl: jest.fn(),
      validateReferralCodeNow: jest.fn(),
      clearReferralCode: jest.fn(),
      register: jest.fn(),
      googleRegister: jest.fn(),
      socialSignUpBlocked: jest.fn(),
    };

    TestBed.configureTestingModule({
      imports: [RegisterComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideMockStore({ selectors: [{ selector: selectCustomerLoading, value: false }] }),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({}) } } },
        { provide: GOOGLE_CLIENT_ID, useValue: 'gsi-client' },
      ],
    }).overrideComponent(RegisterComponent, {
      set: { providers: [{ provide: RegisterFacade, useValue: facade }] },
    });

    const translate = TestBed.inject(TranslateService);
    translate.use(lang);
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    return { fixture, translate };
  }

  it('is drawn in the language of the page', () => {
    render('sk');

    expect(renderButton).toHaveBeenCalledTimes(1);
    expect(renderButton).toHaveBeenCalledWith(expect.any(HTMLElement), expect.objectContaining({ locale: 'sk' }));
  });

  it('is drawn again, alone, when the page changes language', () => {
    const { fixture, translate } = render('sk');

    translate.use('uk');

    expect(renderButton).toHaveBeenCalledTimes(2);
    expect(renderButton).toHaveBeenLastCalledWith(expect.any(HTMLElement), expect.objectContaining({ locale: 'uk' }));
    const container = (fixture.nativeElement as HTMLElement).querySelector('.cl-auth__google');
    expect(container?.querySelectorAll('iframe')).toHaveLength(1);
  });
});
