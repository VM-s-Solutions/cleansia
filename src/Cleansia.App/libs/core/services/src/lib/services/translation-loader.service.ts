import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { TranslateLoader, TranslateService } from '@ngx-translate/core';
import { Observable, firstValueFrom, of } from 'rxjs';

export class JsonTranslationLoader implements TranslateLoader {
  constructor(
    private http: HttpClient,
    private isBrowser: boolean
  ) {}

  getTranslation(lang: string): Observable<any> {
    // On the server (SSR), return empty translations to avoid HTTP requests
    // that would hit EasyAuth and fail with 401.
    if (!this.isBrowser) {
      return of({});
    }
    return this.http.get(`/assets/i18n/${lang}.json`);
  }
}

export function initializeTranslations(
  translate: TranslateService,
  platformId: object
): () => Promise<void> {
  return async () => {
    const isBrowser = isPlatformBrowser(platformId);
    const supported = ['cs', 'en', 'sk', 'uk', 'ru'];

    translate.addLangs(supported);
    translate.setDefaultLang('en');

    if (!isBrowser) {
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
