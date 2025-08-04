import {
  ApplicationConfig,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { CleansiaPreset } from '@cleansia/assets';
import { providePrimeNG } from 'primeng/config';
import { appRoutes } from './app.routes';
import { MessageService } from 'primeng/api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),
    provideAnimationsAsync(),
    providePrimeNG({ theme: { preset: CleansiaPreset, options: { darkModeSelector: false } },  }),
    MessageService
  ],
};
