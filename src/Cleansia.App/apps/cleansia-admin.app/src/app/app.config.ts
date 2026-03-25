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
import localeRu from '@angular/common/locales/ru';
import localeSk from '@angular/common/locales/sk';
import localeUk from '@angular/common/locales/uk';
import {
  APP_INITIALIZER,
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
  initializeTranslations,
  JsonTranslationLoader,
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
    {
      provide: APP_INITIALIZER,
      useFactory: initializeTranslations,
      deps: [TranslateService],
      multi: true,
    },
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
