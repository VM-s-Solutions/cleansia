import { isPlatformBrowser, registerLocaleData } from '@angular/common';
import {
  HttpClient,
  provideHttpClient,
  withFetch,
  withInterceptors,
  withJsonpSupport,
} from '@angular/common/http';
import localeCs from '@angular/common/locales/cs';
import localeEn from '@angular/common/locales/en';
import localeRu from '@angular/common/locales/ru';
import localeSk from '@angular/common/locales/sk';
import localeUk from '@angular/common/locales/uk';
import {
  APP_INITIALIZER,
  ApplicationConfig,
  ErrorHandler,
  importProvidersFrom,
  LOCALE_ID,
  PLATFORM_ID,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, Router, withInMemoryScrolling } from '@angular/router';
import { CleansiaPreset } from '@cleansia/assets';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
import { customerEffects, customerReducers } from '@cleansia/customer-stores';
import {
  APPLE_CLIENT_ID,
  AUTH_COOKIE_KEYS,
  GOOGLE_CLIENT_ID,
  initializeTranslations,
  JsonTranslationLoader,
  MAPBOX_AUTOCOMPLETE_ENABLED,
} from '@cleansia/services';
import { EffectsModule } from '@ngrx/effects';
import { provideStore, StoreModule } from '@ngrx/store';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import * as Sentry from '@sentry/angular';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { environment } from '../environments/environment';
import { appRoutes } from './app.routes';
import { APP_INTERCEPTORS_FN } from './http-interceptors';
import { SESSION_LIFECYCLE_PROVIDERS } from './session-listeners';

registerLocaleData(localeCs);
registerLocaleData(localeEn);
registerLocaleData(localeSk);
registerLocaleData(localeUk);
registerLocaleData(localeRu);

export const appConfig: ApplicationConfig = {
  providers: [
    SESSION_LIFECYCLE_PROVIDERS,
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Reuse the server-rendered DOM instead of destroying and re-rendering it
    // on bootstrap — without this every SSR page repaints from scratch (huge
    // layout shift). Also transfer-caches SSR HTTP responses into the page.
    provideClientHydration(withEventReplay()),
    provideRouter(appRoutes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: { preset: CleansiaPreset, options: { darkModeSelector: '.dark-mode' } },
    }),
    importProvidersFrom(
      TranslateModule.forRoot({
        loader: {
          provide: TranslateLoader,
          useFactory: (http: HttpClient, platformId: object) =>
            new JsonTranslationLoader(http, isPlatformBrowser(platformId)),
          deps: [HttpClient, PLATFORM_ID],
        },
      })
    ),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeTranslations,
      deps: [TranslateService, PLATFORM_ID],
      multi: true,
    },
    MessageService,
    ConfirmationService,
    provideHttpClient(
      withFetch(),
      withJsonpSupport(),
      withInterceptors(APP_INTERCEPTORS_FN)
    ),
    {
      provide: ErrorHandler,
      useValue: Sentry.createErrorHandler({ showDialog: false }),
    },
    { provide: Sentry.TraceService, deps: [Router] },
    { provide: LOCALE_ID, useValue: 'en' },
    { provide: CUSTOMER_API_BASE_URL, useValue: environment.apiBaseUrl },
    // The Mapbox token is NOT shipped to the browser. We only
    // advertise (token-free) whether geocoding is configured; the same-origin
    // proxy (server.ts) injects the credential server-side.
    {
      provide: MAPBOX_AUTOCOMPLETE_ENABLED,
      useValue: !!(environment.mapboxToken ?? '').trim(),
    },
    // The GSI client id is public by design (Google gates access on the page
    // origin, not on secrecy), but it is per-deployment: hard-coding one id in
    // the login/register components made every build advertise the local-dev
    // client, which 403s "origin not allowed" on any other host. Empty here
    // means "not configured" and the components hide the button entirely.
    {
      provide: GOOGLE_CLIENT_ID,
      useValue: (environment.googleClientId ?? '').trim(),
    },
    // Same contract as the GSI id above, and the same reason for the empty
    // default — Apple gates on the domains registered against the Services ID,
    // so a value that is right for one deployment is a dead button on another.
    {
      provide: APPLE_CLIENT_ID,
      useValue: (environment.appleClientId ?? '').trim(),
    },
    {
      provide: AUTH_COOKIE_KEYS,
      useValue: {
        token: 'customer_token',
        refreshToken: 'customer_refresh_token',
        refreshTokenExp: 'customer_refresh_exp',
        role: 'customer_role',
        csrfToken: 'customer_csrf',
      },
    },
    provideStore(),
    importProvidersFrom(
      BrowserAnimationsModule,
      StoreModule.forRoot(customerReducers, {
        runtimeChecks: {
          strictStateImmutability: true,
          strictActionImmutability: true,
        },
      }),
      EffectsModule.forRoot(customerEffects),
      ...(!environment.isDevelopment
        ? []
        : [StoreDevtoolsModule.instrument({ maxAge: 25 })])
    ),
  ],
};
