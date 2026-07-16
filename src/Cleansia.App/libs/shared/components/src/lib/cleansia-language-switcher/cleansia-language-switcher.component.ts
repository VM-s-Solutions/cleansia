import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, PLATFORM_ID } from '@angular/core';
import { FormsModule } from '@angular/forms';
// Inlined from @cleansia/services to avoid module boundary issues
const PREFERRED_LANGUAGE_KEY = 'preferred_language';
import { TranslateService } from '@ngx-translate/core';
import { SelectModule } from 'primeng/select';

interface LanguageOption {
  value: string;
  label: string;
  short: string;
  icon: string;
}

@Component({
  selector: 'cleansia-language-switcher',
  templateUrl: './cleansia-language-switcher.component.html',
  standalone: true,
  imports: [FormsModule, CommonModule, SelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaLanguageSwitcherComponent implements OnInit {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  languages: LanguageOption[] = [];
  selectedLanguage: string;

  private iconMap: { [key: string]: string } = {
    en: 'us',
    cs: 'cz',
    sk: 'sk',
    uk: 'ua',
    ru: 'ru',
  };

  private shortMap: { [key: string]: string } = {
    cs: 'CZ',
    sk: 'SK',
    uk: 'UA',
    ru: 'RU',
  };

  constructor(private translate: TranslateService) {
    this.selectedLanguage =
      this.translate.currentLang || this.translate.getDefaultLang();
  }

  ngOnInit(): void {
    this.languages = this.translate.getLangs().map((lang) => ({
      value: lang,
      label: this.getNativeLanguageName(lang),
      short: this.shortMap[lang] || lang.toUpperCase(),
      icon: this.iconMap[lang] || lang.toLowerCase(),
    }));
  }

  private getNativeLanguageName(lang: string): string {
    try {
      const displayNames = new Intl.DisplayNames([lang], { type: 'language' });
      const name = displayNames.of(lang);
      if (name && name !== lang) {
        return name.charAt(0).toUpperCase() + name.slice(1);
      }
    } catch (e) {
      // If locale not supported or error
    }
    return lang.toUpperCase();
  }

  changeLanguage(lang: string): void {
    this.translate.use(lang);
    if (this.isBrowser) {
      localStorage.setItem(PREFERRED_LANGUAGE_KEY, lang);
      // Cookie mirrors localStorage so SSR renders the next visit in the
      // chosen language instead of English.
      document.cookie = `${PREFERRED_LANGUAGE_KEY}=${lang}; path=/; max-age=31536000; SameSite=Lax`;
      document.documentElement.lang = lang;
    }
  }
}
