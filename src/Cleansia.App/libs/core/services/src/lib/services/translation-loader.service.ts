import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { TranslateLoader, TranslateService } from '@ngx-translate/core';
import { Observable, firstValueFrom } from 'rxjs';

export class JsonTranslationLoader implements TranslateLoader {
  constructor(private http: HttpClient) {}

  getTranslation(lang: string): Observable<any> {
    return this.http.get(`/assets/i18n/${lang}.json`);
  }
}

export function initializeTranslations(
  translate: TranslateService,
  platformId: object
): () => Promise<void> {
  return async () => {
    const supported = ['cs', 'en', 'sk', 'uk', 'ru'];
    translate.addLangs(supported);
    translate.setDefaultLang('en');

    // On server (SSR), skip loading — translations will load on the client after hydration.
    // Loading on the server would hit the full URL and may fail with 401 from Azure auth.
    if (!isPlatformBrowser(platformId)) {
      return;
    }

    const stored = localStorage.getItem('preferred_language');
    const browserLang = navigator.language?.split('-')[0]?.toLowerCase();

    const lang =
      (stored && supported.includes(stored) ? stored : null) ??
      (browserLang && supported.includes(browserLang) ? browserLang : null) ??
      'en';

    await firstValueFrom(translate.use(lang));
  };
}
