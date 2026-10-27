import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { selectCustomerLoading } from '@cleansia/customer-stores';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule } from '@ngx-translate/core';
import { ConfirmEmailComponent } from './confirm-email.component';
import { ConfirmEmailFacade } from './confirm-email.facade';

/**
 * The confirmation sets the password its confirmer chooses (owner ruling 2026-10-26, "Close"). The facade
 * decides whether this visit must ask for one; the screen has to draw the fields exactly then, or Verify
 * answers pages.email_confirm.password_invalid with nowhere to type the password.
 */
describe('ConfirmEmailComponent (customer): the password it asks for', () => {
  function render(passwordAsked: boolean): HTMLElement {
    const facade = {
      formGroup: new FormGroup({
        code: new FormControl<string | null>(null),
        email: new FormControl<string | null>('jan@example.com'),
        password: new FormControl<string | null>(null),
        confirmPassword: new FormControl<string | null>(null),
      }),
      emailKnown: signal(true),
      passwordAsked: signal(passwordAsked),
      isResendDisabled: signal(false),
      resendCodeTimeout: signal(30),
      setEmail: jest.fn(),
      confirmEmail: jest.fn(),
      confirmWhenComplete: jest.fn(),
      resendCode: jest.fn(),
    };

    TestBed.configureTestingModule({
      imports: [ConfirmEmailComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideMockStore({ selectors: [{ selector: selectCustomerLoading, value: false }] }),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    }).overrideComponent(ConfirmEmailComponent, {
      set: { providers: [{ provide: ConfirmEmailFacade, useValue: facade }] },
    });

    const fixture = TestBed.createComponent(ConfirmEmailComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('draws the password lead, the password and its confirmation when the facade asks for one', () => {
    const page = render(true);

    expect(page.textContent).toContain('pages.email_confirm.password_lead');
    expect(page.querySelector('cleansia-text-input[formcontrolname="password"]')).not.toBeNull();
    expect(page.querySelector('cleansia-text-input[formcontrolname="confirmPassword"]')).not.toBeNull();
  });

  it('draws none of them when the page holds the password just registered with', () => {
    const page = render(false);

    expect(page.textContent).not.toContain('pages.email_confirm.password_lead');
    expect(page.querySelector('[formcontrolname="password"]')).toBeNull();
    expect(page.querySelector('[formcontrolname="confirmPassword"]')).toBeNull();
  });
});
