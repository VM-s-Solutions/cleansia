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

describe('partner language preference sync wiring', () => {
  let start: jest.Mock;

  beforeEach(() => {
    start = jest.fn();

    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: PartnerLanguagePreferenceSyncService, useValue: { start } },
        { provide: Store, useValue: { dispatch: jest.fn(), select: () => of(null) } },
        { provide: Router, useValue: { events: EMPTY, url: '/dashboard' } },
        {
          provide: PartnerAuthService,
          useValue: { isLoggedIn$: new BehaviorSubject(false), logout: () => EMPTY },
        },
        {
          provide: RegistrationCompletionService,
          useValue: { isRegistrationComplete: () => true },
        },
        { provide: PageTitleService, useValue: { initialize: jest.fn() } },
        { provide: DialogService, useValue: { confirmTranslated: () => EMPTY } },
        {
          provide: TranslateService,
          useValue: { currentLang: 'en', getDefaultLang: () => 'en', instant: (k: string) => k },
        },
      ],
    }).overrideComponent(AppComponent, { set: { template: '', imports: [] } });
  });

  it('starts the language push seam when the app starts', () => {
    TestBed.createComponent(AppComponent).componentInstance.ngOnInit();

    expect(start).toHaveBeenCalledTimes(1);
  });
});
