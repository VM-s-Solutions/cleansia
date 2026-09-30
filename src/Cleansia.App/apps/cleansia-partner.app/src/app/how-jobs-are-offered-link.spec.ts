import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  PartnerAuthService,
  PartnerLanguagePreferenceSyncService,
  RegistrationCompletionService,
} from '@cleansia/partner-services';
import { DialogService, PageTitleService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, EMPTY, of } from 'rxjs';
import { AppComponent } from './app.component';
import { appRoutes, HOW_JOBS_ARE_OFFERED_PATH } from './app.routes';

/**
 * The partner mobile apps open this page by its URL in a browser, where the cleaner may not be signed
 * in, and it states the approval criteria, so a cleaner still completing registration must read it.
 */
describe('how jobs are offered, from the partner shell', () => {
  const createShell = (url: string): AppComponent => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: PartnerLanguagePreferenceSyncService, useValue: { start: jest.fn() } },
        {
          provide: Store,
          useValue: { dispatch: jest.fn(), select: () => of({ isEmailConfirmed: true }) },
        },
        { provide: Router, useValue: { events: EMPTY, url } },
        {
          provide: PartnerAuthService,
          useValue: { isLoggedIn$: new BehaviorSubject(true), logout: () => EMPTY },
        },
        {
          provide: RegistrationCompletionService,
          useValue: { isRegistrationComplete: () => false },
        },
        { provide: PageTitleService, useValue: { initialize: jest.fn() } },
        { provide: DialogService, useValue: { confirmTranslated: () => EMPTY } },
        {
          provide: TranslateService,
          useValue: { currentLang: 'en', getDefaultLang: () => 'en', instant: (k: string) => k },
        },
      ],
    }).overrideComponent(AppComponent, { set: { template: '', imports: [] } });

    const shell = TestBed.createComponent(AppComponent).componentInstance;
    shell.ngOnInit();
    return shell;
  };

  afterEach(() => TestBed.resetTestingModule());

  it('keeps the address the mobile apps link to, with no guard in front of it', () => {
    const route = appRoutes.find((candidate) => candidate.path === HOW_JOBS_ARE_OFFERED_PATH);

    expect(HOW_JOBS_ARE_OFFERED_PATH).toBe('how-jobs-are-offered');
    expect(route?.loadChildren).toBeDefined();
    expect(route?.canActivate).toBeUndefined();
  });

  it('is linked from the sidebar', () => {
    const shell = createShell('/dashboard');

    expect(shell.sidebarMenuItems).toContainEqual(
      expect.objectContaining({
        label: 'sidebar.how_jobs_are_offered',
        route: '/how-jobs-are-offered',
      })
    );
  });

  it('is not covered by the registration lock that covers the dashboard', () => {
    expect(createShell('/dashboard').shouldShowRegistrationLock()).toBe(true);
    TestBed.resetTestingModule();

    expect(createShell('/how-jobs-are-offered').shouldShowRegistrationLock()).toBe(false);
  });
});
