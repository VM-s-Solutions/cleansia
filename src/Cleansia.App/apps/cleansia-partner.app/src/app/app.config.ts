import { registerLocaleData } from '@angular/common';
import {
  HttpClient,
  provideHttpClient,
  withFetch,
  withInterceptors,
  withJsonpSupport,
} from '@angular/common/http';
import localeCs from '@angular/common/locales/cs';
import {
  ApplicationConfig,
  importProvidersFrom,
  LOCALE_ID,
  provideZoneChangeDetection,
} from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { CleansiaPreset } from '@cleansia/assets';
import { INTERCEPTORS_FN, JsonTranslationLoader } from '@cleansia/services';
import { effects, reducers } from '@cleansia/stores';
import { EffectsModule } from '@ngrx/effects';
import { provideStore, StoreModule } from '@ngrx/store';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import { TranslateLoader, TranslateModule } from '@ngx-translate/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { environment } from '../environments/environment.prod';
import { appRoutes } from './app.routes';

registerLocaleData(localeCs);

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: { preset: CleansiaPreset, options: { darkModeSelector: false } },
    }),
    provideCharts(withDefaultRegisterables()),
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
      withInterceptors(INTERCEPTORS_FN)
    ),
    { provide: LOCALE_ID, useValue: 'cs' },
    provideStore(),
    importProvidersFrom(
      BrowserAnimationsModule,
      StoreModule.forRoot(reducers, {
        runtimeChecks: {
          strictStateImmutability: true,
          strictActionImmutability: true,
        },
      }),
      EffectsModule.forRoot(effects),
      ...(!environment.isDevelopment
        ? []
        : [StoreDevtoolsModule.instrument({ maxAge: 25 })])
    ),
  ],
};
