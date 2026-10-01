import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Select } from 'primeng/select';
import { CleansiaLanguageSwitcherComponent } from './cleansia-language-switcher.component';

describe('CleansiaLanguageSwitcherComponent', () => {
  let translate: TranslateService;
  let component: CleansiaLanguageSwitcherComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaLanguageSwitcherComponent, TranslateModule.forRoot()],
      providers: [provideNoopAnimations()],
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

  describe('globe variant, list open', () => {
    let fixture: ComponentFixture<CleansiaLanguageSwitcherComponent>;

    const settle = async (): Promise<void> => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const options = (): HTMLElement[] =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.p-select-option'));

    beforeEach(async () => {
      // Opening the list asks matchMedia whether to go modal and scrolls the selected row into
      // view; jsdom implements neither.
      if (!window.matchMedia) {
        window.matchMedia = (media: string) => ({ matches: false, media }) as MediaQueryList;
      }
      if (!Element.prototype.scrollIntoView) {
        Element.prototype.scrollIntoView = () => undefined;
      }
      fixture = TestBed.createComponent(CleansiaLanguageSwitcherComponent);
      fixture.componentRef.setInput('variant', 'globe');
      await settle();
      (fixture.debugElement.query(By.directive(Select)).componentInstance as Select).show();
      await settle();
    });

    it('draws no globe in any row; the globe belongs to the trigger', () => {
      expect(options()).toHaveLength(5);
      expect(options().some((row) => row.querySelector('.pi-globe'))).toBe(false);
      expect((fixture.nativeElement as HTMLElement).querySelector('.p-select-label .pi-globe')).not.toBeNull();
    });

    it('ticks exactly one row, the current language, named natively beside its code', () => {
      const ticked = options().filter((row) => row.querySelector('.pi-check'));

      expect(ticked).toHaveLength(1);
      expect(ticked[0].getAttribute('aria-selected')).toBe('true');
      expect(ticked[0].querySelector('.cleansia-language-switcher__name')?.textContent?.trim()).toBe('English');
      expect(ticked[0].querySelector('.cleansia-language-switcher__code')?.textContent?.trim()).toBe('EN');
    });
  });
});
