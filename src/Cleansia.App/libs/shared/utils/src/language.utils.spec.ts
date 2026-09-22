import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { currentLanguage } from './language.utils';

describe('currentLanguage', () => {
  it('starts on the session language and follows every switch', () => {
    TestBed.configureTestingModule({ imports: [TranslateModule.forRoot()] });
    const translate = TestBed.inject(TranslateService);
    translate.use('cs');

    const lang = runInInjectionContext(TestBed.inject(Injector), () => currentLanguage(translate));
    expect(lang()).toBe('cs');

    translate.use('uk');
    expect(lang()).toBe('uk');
  });
});
