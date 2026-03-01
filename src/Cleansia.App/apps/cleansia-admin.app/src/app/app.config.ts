import { registerLocaleData } from '@angular/common';
import {
  HttpClient,
  provideHttpClient,
  withFetch,
  withInterceptors,
  withJsonpSupport,
} from '@angular/common/http';
import localeCs from '@angular/common/locales/cs';
import localeEn from '@angular/common/locales/en';
import localePl from '@angular/common/locales/pl';
import {
  ApplicationConfig,
  ErrorHandler,
  importProvidersFrom,
  LOCALE_ID,
  provideZoneChangeDetection,
} from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, Router } from '@angular/router';
import {
  ADMIN_INTERCEPTORS_FN,
  ADMINAPIBASEURL,
} from '@cleansia/admin-services';
import { adminEffects, adminReducers } from '@cleansia/admin-stores';
import { CleansiaPreset } from '@cleansia/assets';
import {
  COMMON_INTERCEPTORS_FN,
  JsonTranslationLoader,
} from '@cleansia/services';
import { EffectsModule } from '@ngrx/effects';
import { provideStore, StoreModule } from '@ngrx/store';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import { TranslateLoader, TranslateModule } from '@ngx-translate/core';
import * as Sentry from '@sentry/angular';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { environment } from '../environments/environment';
import { appRoutes } from './app.routes';

registerLocaleData(localeCs);
registerLocaleData(localeEn);
registerLocaleData(localePl);

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: { preset: CleansiaPreset, options: { darkModeSelector: false } },
    }),
    importProvidersFrom(
      TranslateModule.forRoot({
        loader: {
          provide: TranslateLoader,
          useFactory: (http: HttpClient) => new JsonTranslationLoader(http),
          deps: [HttpClient],
        },
      })
    ),
    MessageService,
    ConfirmationService,
    provideHttpClient(
      withFetch(),
      withJsonpSupport(),
      withInterceptors([...COMMON_INTERCEPTORS_FN, ...ADMIN_INTERCEPTORS_FN])
    ),
    { provide: ErrorHandler, useValue: Sentry.createErrorHandler({ showDialog: false }) },
    { provide: Sentry.TraceService, deps: [Router] },
    { provide: LOCALE_ID, useValue: 'en' },
    { provide: ADMINAPIBASEURL, useValue: environment.apiBaseUrl },
    provideStore(),
    importProvidersFrom(
      BrowserAnimationsModule,
      StoreModule.forRoot(adminReducers, {
        runtimeChecks: {
          strictStateImmutability: true,
          strictActionImmutability: true,
        },
      }),
      EffectsModule.forRoot(adminEffects),
      ...(!environment.isDevelopment
        ? []
        : [StoreDevtoolsModule.instrument({ maxAge: 25 })])
    ),
  ],
};
