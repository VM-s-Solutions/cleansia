import {
  ApplicationConfig,
  inject,
  mergeApplicationConfig,
  REQUEST,
} from '@angular/core';
import { provideServerRendering } from '@angular/platform-server';
import { provideServerRoutesConfig } from '@angular/ssr';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
import { TranslateLoader } from '@ngx-translate/core';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { of, type Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';

/**
 * Server-side translation loader — reads the i18n JSON straight from disk.
 * The browser loader is skipped on the server (it would issue an HTTP call),
 * but returning `{}` made SSR render raw translation keys: bad for SEO and a
 * guaranteed layout shift when the client swaps the real text in.
 */
class ServerJsonTranslationLoader implements TranslateLoader {
  getTranslation(lang: string): Observable<Record<string, unknown>> {
    // Built layout: dist/<app>/server/*.mjs next to dist/<app>/browser/assets.
    // The source path covers the dev-server, where no dist assets exist.
    const candidates = [
      join(dirname(fileURLToPath(import.meta.url)), '../browser/assets/i18n', `${lang}.json`),
      join(process.cwd(), 'apps/cleansia.app/src/assets/i18n', `${lang}.json`),
    ];
    for (const path of candidates) {
      try {
        if (existsSync(path)) {
          return of(JSON.parse(readFileSync(path, 'utf-8')));
        }
      } catch {
        // fall through to the next candidate
      }
    }
    return of({});
  }
}

const serverAppConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(),
    provideServerRoutesConfig(serverRoutes),
    { provide: TranslateLoader, useClass: ServerJsonTranslationLoader },
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
