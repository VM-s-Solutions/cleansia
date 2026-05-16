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
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, Router, withInMemoryScrolling } from '@angular/router';
import { CleansiaPreset } from '@cleansia/assets';
import {
  CUSTOMER_API_BASE_URL,
  CUSTOMER_INTERCEPTORS_FN,
} from '@cleansia/customer-services';
import { customerEffects, customerReducers } from '@cleansia/customer-stores';
import {
  AUTH_COOKIE_KEYS,
  COMMON_INTERCEPTORS_FN,
  initializeTranslations,
  JsonTranslationLoader,
  MAPBOX_ACCESS_TOKEN,
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

registerLocaleData(localeCs);
registerLocaleData(localeEn);
registerLocaleData(localeSk);
registerLocaleData(localeUk);
registerLocaleData(localeRu);

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
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
      withInterceptors([
        ...COMMON_INTERCEPTORS_FN,
        ...CUSTOMER_INTERCEPTORS_FN,
      ])
    ),
    {
      provide: ErrorHandler,
      useValue: Sentry.createErrorHandler({ showDialog: false }),
    },
    { provide: Sentry.TraceService, deps: [Router] },
    { provide: LOCALE_ID, useValue: 'en' },
    { provide: CUSTOMER_API_BASE_URL, useValue: environment.apiBaseUrl },
    { provide: MAPBOX_ACCESS_TOKEN, useValue: environment.mapboxToken ?? '' },
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
