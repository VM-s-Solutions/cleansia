import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { loadCustomerUser } from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { of, Subject, throwError } from 'rxjs';
import { ConfirmEmailFacade } from './confirm-email.facade';

describe('ConfirmEmailFacade (customer)', () => {
  let facade: ConfirmEmailFacade;
  let store: MockStore;
  let authService: {
    confirmUserEmail: jest.Mock;
    resendEmailConfirmation: jest.Mock;
    holdsRegisteredPasswordFor: jest.Mock;
  };
  let snackbar: {
    showApiError: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    // By default the page has just registered the address, so it holds the password typed there.
    authService = {
      confirmUserEmail: jest.fn(),
      resendEmailConfirmation: jest.fn(),
      holdsRegisteredPasswordFor: jest.fn().mockReturnValue(true),
    };
    snackbar = {
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ConfirmEmailFacade,
        provideMockStore(),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });

    facade = TestBed.inject(ConfirmEmailFacade);
    store = TestBed.inject(MockStore);
  });

  it('sends BOTH the code and the known email to the auth service', () => {
    authService.confirmUserEmail.mockReturnValue(of({}));
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(authService.confirmUserEmail).toHaveBeenCalledWith(
      '123456',
      'jan@example.com',
      undefined
    );
  });

  it('sends the manually typed email when navigation did not provide one', () => {
    authService.confirmUserEmail.mockReturnValue(of({}));
    facade.formGroup.patchValue({
      code: '654321',
      email: 'typed@example.com',
      password: 'Heslo2026',
      confirmPassword: 'Heslo2026',
    });

    facade.confirmEmail();

    expect(authService.confirmUserEmail).toHaveBeenCalledWith(
      '654321',
      'typed@example.com',
      'Heslo2026'
    );
  });

  // Owner ruling 2026-10-26 ("Close"): the confirmation sets the password its confirmer chose. Right
  // after registering that is the password just typed; any other time this screen asks for one.
  describe('the password the confirmation sets', () => {
    const fill = (password: string, confirmPassword = password) =>
      facade.formGroup.patchValue({ code: '123456', password, confirmPassword });

    it('asks for none right after this page registered the address, and leaves it to the one typed there', () => {
      facade.setEmail('jan@example.com');

      expect(authService.holdsRegisteredPasswordFor).toHaveBeenCalledWith('jan@example.com');
      expect(facade.passwordAsked()).toBe(false);
    });

    it('asks for one when it holds none, as after a reload or a sign-in to an unconfirmed account', () => {
      authService.holdsRegisteredPasswordFor.mockReturnValue(false);

      facade.setEmail('jan@example.com');

      expect(facade.passwordAsked()).toBe(true);
    });

    it('asks for one when the e-mail is typed rather than given', () => {
      expect(facade.passwordAsked()).toBe(true);
    });

    it('sends the password chosen, once it meets the rules and is typed the same twice', () => {
      authService.holdsRegisteredPasswordFor.mockReturnValue(false);
      authService.confirmUserEmail.mockReturnValue(of({}));
      facade.setEmail('jan@example.com');
      fill('Heslo2026');

      facade.confirmEmail();

      expect(authService.confirmUserEmail).toHaveBeenCalledWith('123456', 'jan@example.com', 'Heslo2026');
    });

    it.each([
      ['breaks the rules', 'heslo', 'heslo'],
      ['is typed differently the second time', 'Heslo2026', 'Heslo2027'],
      ['is not typed the second time', 'Heslo2026', ''],
    ])('refuses a password that %s, saying so', (_case, password, confirmPassword) => {
      authService.holdsRegisteredPasswordFor.mockReturnValue(false);
      facade.setEmail('jan@example.com');
      fill(password, confirmPassword);

      facade.confirmEmail();

      expect(authService.confirmUserEmail).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith('pages.email_confirm.password_invalid');
    });

    it('confirms as the code fills in when nothing else is missing', () => {
      authService.confirmUserEmail.mockReturnValue(of({}));
      facade.setEmail('jan@example.com');
      facade.formGroup.get('code')?.setValue('123456');

      facade.confirmWhenComplete();

      expect(authService.confirmUserEmail).toHaveBeenCalledTimes(1);
    });

    it('waits for the button, saying nothing, while a password is still to choose', () => {
      authService.holdsRegisteredPasswordFor.mockReturnValue(false);
      facade.setEmail('jan@example.com');
      facade.formGroup.get('code')?.setValue('123456');

      facade.confirmWhenComplete();

      expect(authService.confirmUserEmail).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    });
  });

  it('keeps the email field visible when the query param is not a valid email', () => {
    facade.setEmail('not-an-email');

    expect(facade.emailKnown()).toBe(false);
    expect(facade.formGroup.get('email')?.value).toBe('not-an-email');
  });

  it('marks the email as known once navigation provides it', () => {
    expect(facade.emailKnown()).toBe(false);

    facade.setEmail('jan@example.com');

    expect(facade.emailKnown()).toBe(true);
    expect(facade.formGroup.get('email')?.value).toBe('jan@example.com');
  });

  it('does not call the service and shows a validation snackbar when the email is missing', () => {
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(authService.confirmUserEmail).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'validation.common.not_all_fields_filled'
    );
  });

  it('on success: shows the success snackbar, reloads the user and navigates to orders', () => {
    authService.confirmUserEmail.mockReturnValue(of({}));
    const dispatchSpy = jest.spyOn(store, 'dispatch');
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'auth.confirm_email.success'
    );
    expect(dispatchSpy).toHaveBeenCalledWith(loadCustomerUser());
    expect(router.navigate).toHaveBeenCalledWith([CleansiaCustomerRoute.ORDERS]);
  });

  it('maps a failed confirmation to showApiError with the confirm fallback key', () => {
    authService.confirmUserEmail.mockReturnValue(
      throwError(() => ({ status: 400 }))
    );
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(snackbar.showApiError).toHaveBeenCalledWith(
      expect.anything(),
      'auth.confirm_email.error'
    );
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('ignores a confirm while one is in flight, so a code completed twice is sent once', () => {
    const pending = new Subject<unknown>();
    authService.confirmUserEmail.mockReturnValue(pending);
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();
    facade.confirmEmail();

    expect(authService.confirmUserEmail).toHaveBeenCalledTimes(1);
    expect(facade.confirming()).toBe(true);

    pending.next({});
    pending.complete();

    expect(router.navigate).toHaveBeenCalledTimes(1);
    expect(facade.confirming()).toBe(false);
  });

  it('sends the code again once the confirm in flight has been refused', () => {
    const pending = new Subject<unknown>();
    authService.confirmUserEmail.mockReturnValueOnce(pending).mockReturnValue(of({}));
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();
    pending.error({ status: 400 });
    facade.confirmEmail();

    expect(snackbar.showApiError).toHaveBeenCalledTimes(1);
    expect(authService.confirmUserEmail).toHaveBeenCalledTimes(2);
    expect(facade.confirming()).toBe(false);
  });

  it('resends using the email from state and runs the cooldown', () => {
    jest.useFakeTimers();
    authService.resendEmailConfirmation.mockReturnValue(of(true));
    facade.setEmail('jan@example.com');

    facade.resendCode();

    expect(authService.resendEmailConfirmation).toHaveBeenCalledWith(
      'jan@example.com'
    );
    expect(facade.isResendDisabled()).toBe(true);

    jest.advanceTimersByTime(30000);

    expect(facade.isResendDisabled()).toBe(false);
    jest.useRealTimers();
  });

  it('does not resend when no valid email is available', () => {
    facade.resendCode();

    expect(authService.resendEmailConfirmation).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'validation.common.not_all_fields_filled'
    );
    expect(facade.isResendDisabled()).toBe(false);
  });
});
