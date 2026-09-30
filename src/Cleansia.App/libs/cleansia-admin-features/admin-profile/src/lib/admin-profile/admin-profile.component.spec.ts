import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { AdminAuthService, AdminClient } from '@cleansia/admin-services';
import { CleansiaButtonComponent } from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { AdminProfileComponent } from './admin-profile.component';

/**
 * The profile's only content is the password change, which is why the admin guard holds an
 * administrator whose password someone else chose here: the page then says why, and carries the
 * sign-out the hidden sidebar would otherwise offer.
 */
describe('AdminProfileComponent', () => {
  let logout: jest.Mock;

  function render(passwordChangeRequired: boolean): ComponentFixture<AdminProfileComponent> {
    logout = jest.fn(() => of(true));

    TestBed.configureTestingModule({
      imports: [AdminProfileComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: AdminClient, useValue: { adminAuthClient: { changePassword: jest.fn() } } },
        {
          provide: AdminAuthService,
          useValue: {
            passwordChangeRequired: signal(passwordChangeRequired),
            clearPasswordChangeRequired: jest.fn(),
            logout,
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccessTranslated: jest.fn(), showErrorTranslated: jest.fn() },
        },
      ],
    });

    const fixture = TestBed.createComponent(AdminProfileComponent);
    fixture.detectChanges();
    return fixture;
  }

  function buttonLabels(fixture: ComponentFixture<AdminProfileComponent>): string[] {
    return fixture.debugElement
      .queryAll(By.directive(CleansiaButtonComponent))
      .map((b) => (b.componentInstance as CleansiaButtonComponent).label());
  }

  it('is the profile, with no sign-out of its own, when nothing is held', () => {
    const fixture = render(false);
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('pages.admin_profile.title');
    expect(text).not.toContain('pages.admin_profile.password_change_required.description');
    expect(buttonLabels(fixture)).toEqual(['pages.admin_profile.change_password.submit']);
  });

  it('says why the administrator is held and offers a sign-out beside the change', () => {
    const fixture = render(true);
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('pages.admin_profile.password_change_required.title');
    expect(text).toContain('pages.admin_profile.password_change_required.description');
    expect(text).not.toContain('pages.admin_profile.title');
    expect(buttonLabels(fixture)).toEqual([
      'pages.admin_profile.password_change_required.sign_out',
      'pages.admin_profile.change_password.submit',
    ]);
  });

  it('signs the held administrator out from the page', () => {
    const fixture = render(true);
    const signOut = fixture.debugElement
      .queryAll(By.directive(CleansiaButtonComponent))
      .map((b) => b.componentInstance as CleansiaButtonComponent)
      .find((b) => b.label() === 'pages.admin_profile.password_change_required.sign_out');

    signOut?.onClick.emit(new MouseEvent('click'));

    expect(logout).toHaveBeenCalledTimes(1);
  });

  it('asks for the administrator minimum of 12 characters before sending the change', () => {
    const newPassword = render(true).componentInstance.form.controls.newPassword;

    newPassword.setValue('Short4Admin');
    expect(newPassword.valid).toBe(false);

    newPassword.setValue('Long4Enough1');
    expect(newPassword.valid).toBe(true);
  });
});
