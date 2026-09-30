import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AdminAuthService, AdminNotificationBadgeService } from '@cleansia/admin-services';
import { DialogService, PageTitleService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, EMPTY } from 'rxjs';
import { AppComponent } from './app.component';

/**
 * An administrator whose password someone else chose is held on the profile's password change by
 * the admin guard. The shell offers no navigation meanwhile, since every entry would land back on
 * the same page; the held page carries its own sign-out.
 */
describe('admin app shell while the password change is required', () => {
  function create(loggedIn: boolean, passwordChangeRequired: boolean) {
    const required = signal(passwordChangeRequired);
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: Store, useValue: { dispatch: jest.fn() } },
        {
          provide: AdminAuthService,
          useValue: {
            isLoggedIn$: new BehaviorSubject(loggedIn),
            passwordChangeRequired: required,
            logout: () => EMPTY,
          },
        },
        { provide: PageTitleService, useValue: { initialize: jest.fn() } },
        { provide: AdminNotificationBadgeService, useValue: { start: jest.fn(), badgeLabel: () => null } },
        { provide: DialogService, useValue: { confirmTranslated: () => EMPTY } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    }).overrideComponent(AppComponent, { set: { template: '', imports: [] } });

    return { component: TestBed.createComponent(AppComponent).componentInstance, required };
  }

  it('shows the navigation to a signed-in administrator with nothing held', () => {
    expect(create(true, false).component.showNavigation()).toBe(true);
  });

  it('hides the navigation while the administrator is held', () => {
    expect(create(true, true).component.showNavigation()).toBe(false);
  });

  it('brings the navigation back once the change releases the hold', () => {
    const { component, required } = create(true, true);

    required.set(false);

    expect(component.showNavigation()).toBe(true);
  });

  it('shows no navigation to a signed-out visitor', () => {
    expect(create(false, false).component.showNavigation()).toBe(false);
  });
});
