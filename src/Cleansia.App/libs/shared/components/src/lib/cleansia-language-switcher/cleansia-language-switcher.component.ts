import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
// Inlined from @cleansia/services to avoid module boundary issues
const PREFERRED_LANGUAGE_KEY = 'preferred_language';
import { TranslateService } from '@ngx-translate/core';
import { SelectModule } from 'primeng/select';

@Component({
  selector: 'cleansia-language-switcher',
  templateUrl: './cleansia-language-switcher.component.html',
  styleUrls: ['./cleansia-language-switcher.component.scss'],
  standalone: true,
  imports: [FormsModule, CommonModule, SelectModule],
})
export class CleansiaLanguageSwitcherComponent implements OnInit {
  languages: any[] = [];
  selectedLanguage: string;

  private iconMap: { [key: string]: string } = {
    en: 'us',
    cs: 'cz',
  };

  private shortMap: { [key: string]: string } = {
    cs: 'CZ',
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
    localStorage.setItem(PREFERRED_LANGUAGE_KEY, lang);
  }
}
