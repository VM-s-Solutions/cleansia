import {
  ApplicationConfig,
  inject,
  mergeApplicationConfig,
  REQUEST,
} from '@angular/core';
import { provideServerRendering } from '@angular/platform-server';
import { provideServerRoutesConfig } from '@angular/ssr';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
import { environment } from '../environments/environment';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';

const serverAppConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(),
    provideServerRoutesConfig(serverRoutes),
    // Server-side fetch cannot resolve a relative /api URL. In dev the base
    // URL is intentionally empty (same-origin proxy), so SSR resolves it
    // against the incoming request's origin and loops back through the
    // dev-server proxy. Absolute base URLs (staging/prod) pass through.
    {
      provide: CUSTOMER_API_BASE_URL,
      useFactory: () => {
        if (environment.apiBaseUrl) {
          return environment.apiBaseUrl;
        }
        const request = inject(REQUEST, { optional: true });
        return request ? new URL(request.url).origin : '';
      },
    },
  ],
};

export const serverConfig = mergeApplicationConfig(appConfig, serverAppConfig);
