import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { inject, REQUEST } from '@angular/core';
import { TranslateLoader, TranslateService } from '@ngx-translate/core';
import { Observable, firstValueFrom, of } from 'rxjs';

const SUPPORTED_LANGUAGES = ['cs', 'en', 'sk', 'uk', 'ru'];
const PREFERRED_LANGUAGE_KEY = 'preferred_language';

export class JsonTranslationLoader implements TranslateLoader {
  constructor(
    private http: HttpClient,
    private isBrowser: boolean
  ) {}

  getTranslation(lang: string): Observable<Record<string, unknown>> {
    // On the server (SSR), return empty translations to avoid HTTP requests
    // that would hit EasyAuth and fail with 401. The customer SSR app
    // overrides this loader with a disk-reading one in app.config.server.ts.
    if (!this.isBrowser) {
      return of({});
    }
    return this.http.get<Record<string, unknown>>(`/assets/i18n/${lang}.json`);
  }
}

/**
 * Persists the language choice where both renderers can see it: localStorage
 * for the client, and a cookie so the next SSR response is rendered in the
 * same language instead of falling back to English. Browser only.
 */
export function persistPreferredLanguage(lang: string): void {
  localStorage.setItem(PREFERRED_LANGUAGE_KEY, lang);
  document.cookie = `${PREFERRED_LANGUAGE_KEY}=${lang}; path=/; max-age=31536000; SameSite=Lax`;
  document.documentElement.lang = lang;
}

/** Resolves the render language for an SSR request: preference cookie first, then Accept-Language. */
export function resolveRequestLanguage(
  cookieHeader: string | null | undefined,
  acceptLanguageHeader: string | null | undefined
): string {
  const cookieMatch = (cookieHeader ?? '').match(
    /(?:^|;\s*)preferred_language=([a-zA-Z-]+)/
  );
  const fromCookie = cookieMatch?.[1]?.split('-')[0]?.toLowerCase();
  if (fromCookie && SUPPORTED_LANGUAGES.includes(fromCookie)) {
    return fromCookie;
  }
  for (const part of (acceptLanguageHeader ?? '').split(',')) {
    const primary = part.split(';')[0]?.trim().split('-')[0]?.toLowerCase();
    if (primary && SUPPORTED_LANGUAGES.includes(primary)) {
      return primary;
    }
  }
  return 'en';
}

export function initializeTranslations(
  translate: TranslateService,
  platformId: object
): () => Promise<void> {
  const request = inject(REQUEST, { optional: true });
  return async () => {
    const isBrowser = isPlatformBrowser(platformId);

    translate.addLangs(SUPPORTED_LANGUAGES);
    translate.setDefaultLang('en');

    if (!isBrowser) {
      // SSR renders in the visitor's language so hydration doesn't repaint
      // an English page into cs/sk/uk/ru (visible as mixed-language flashes).
      const lang = resolveRequestLanguage(
        request?.headers.get('cookie'),
        request?.headers.get('accept-language')
      );
      await firstValueFrom(translate.use(lang));
      return;
    }

    const stored = localStorage.getItem(PREFERRED_LANGUAGE_KEY);
    const browserLang = navigator.language?.split('-')[0]?.toLowerCase();

    const lang =
      (stored && SUPPORTED_LANGUAGES.includes(stored) ? stored : null) ??
      (browserLang && SUPPORTED_LANGUAGES.includes(browserLang) ? browserLang : null) ??
      'en';

    persistPreferredLanguage(lang);
    await firstValueFrom(translate.use(lang));
  };
}
