import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { GOOGLE_CLIENT_ID } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LoginComponent } from './login.component';
import { LoginFacade } from './login.facade';

type GoogleWindow = Window & { google?: unknown };

/**
 * Google draws its button in the language it is given, else in the browser's or the Google account's,
 * which put a Czech button on an English page.
 */
describe('LoginComponent — the Google button', () => {
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
        email: new FormControl(''),
        password: new FormControl(''),
        rememberMe: new FormControl(false),
      }),
      loading: signal(false),
      login: jest.fn(),
      googleLogin: jest.fn(),
      appleLogin: jest.fn(),
      appleSignInFailed: jest.fn(),
    };

    TestBed.configureTestingModule({
      imports: [LoginComponent, TranslateModule.forRoot()],
      providers: [provideRouter([]), { provide: GOOGLE_CLIENT_ID, useValue: 'gsi-client' }],
    }).overrideComponent(LoginComponent, {
      set: { providers: [{ provide: LoginFacade, useValue: facade }] },
    });

    const translate = TestBed.inject(TranslateService);
    translate.use(lang);
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return { fixture, translate };
  }

  it('is drawn in the language of the page', () => {
    render('cs');

    expect(renderButton).toHaveBeenCalledTimes(1);
    expect(renderButton).toHaveBeenCalledWith(expect.any(HTMLElement), expect.objectContaining({ locale: 'cs' }));
  });

  it('is drawn again, alone, when the page changes language', () => {
    const { fixture, translate } = render('cs');

    translate.use('en');

    expect(renderButton).toHaveBeenCalledTimes(2);
    expect(renderButton).toHaveBeenLastCalledWith(expect.any(HTMLElement), expect.objectContaining({ locale: 'en' }));
    const container = (fixture.nativeElement as HTMLElement).querySelector('.cl-auth__google');
    expect(container?.querySelectorAll('iframe')).toHaveLength(1);
  });

  it('is drawn no more once the page is gone', () => {
    const { fixture, translate } = render('cs');

    fixture.destroy();
    translate.use('en');

    expect(renderButton).toHaveBeenCalledTimes(1);
  });
});
