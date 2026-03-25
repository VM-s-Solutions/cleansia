import { HttpClient } from '@angular/common/http';
import { TranslateLoader, TranslateService } from '@ngx-translate/core';
import { Observable, firstValueFrom } from 'rxjs';

export class JsonTranslationLoader implements TranslateLoader {
  constructor(private http: HttpClient) {}

  getTranslation(lang: string): Observable<any> {
    return this.http.get(`/assets/i18n/${lang}.json`);
  }
}

export function initializeTranslations(translate: TranslateService): () => Promise<void> {
  return async () => {
    const supported = ['cs', 'en', 'sk', 'uk', 'ru'];
    translate.addLangs(supported);
    translate.setDefaultLang('en');

    const stored =
      typeof localStorage !== 'undefined'
        ? localStorage.getItem('preferred_language')
        : null;
    const browserLang =
      typeof navigator !== 'undefined'
        ? navigator.language?.split('-')[0]?.toLowerCase()
        : null;

    const lang =
      (stored && supported.includes(stored) ? stored : null) ??
      (browserLang && supported.includes(browserLang) ? browserLang : null) ??
      'en';

    await firstValueFrom(translate.use(lang));
  };
}
