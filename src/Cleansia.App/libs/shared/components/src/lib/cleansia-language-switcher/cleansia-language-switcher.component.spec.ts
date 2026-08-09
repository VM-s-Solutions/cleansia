import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CleansiaLanguageSwitcherComponent } from './cleansia-language-switcher.component';

describe('CleansiaLanguageSwitcherComponent', () => {
  let translate: TranslateService;
  let component: CleansiaLanguageSwitcherComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaLanguageSwitcherComponent, TranslateModule.forRoot()],
    }).compileComponents();

    translate = TestBed.inject(TranslateService);
    translate.addLangs(['cs', 'en', 'sk', 'uk', 'ru']);
    translate.setDefaultLang('en');
    translate.use('en');

    component = TestBed.createComponent(CleansiaLanguageSwitcherComponent).componentInstance;
  });

  /**
   * The partner app carries the picked language to the server by observing `onLangChange`, which
   * only fires because the pick goes through `TranslateService.use`. Setting `currentLang` directly
   * would still repaint the UI and would silently stop the push.
   */
  it('routes the pick through TranslateService.use, which is what the server-side sync observes', () => {
    const seen: string[] = [];
    translate.onLangChange.subscribe(({ lang }) => seen.push(lang));

    component.changeLanguage('cs');

    expect(seen).toEqual(['cs']);
    expect(translate.currentLang).toBe('cs');
  });

  it('persists the pick locally so the next visit and the next SSR render agree', () => {
    component.changeLanguage('sk');

    expect(localStorage.getItem('preferred_language')).toBe('sk');
    expect(document.cookie).toContain('preferred_language=sk');
    expect(document.documentElement.lang).toBe('sk');
  });
});
