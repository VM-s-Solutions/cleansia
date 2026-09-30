import { PLATFORM_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { CleansiaCookieConsentComponent } from './cleansia-cookie-consent.component';

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const CUSTOMER_I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia.app/src/assets/i18n');
const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'];
const NOTICE_KEYS = ['acknowledge', 'banner_title', 'description', 'learn_more', 'title'];
const STORAGE_KEY = 'spec-cookie-notice';

describe('the cookie notice', () => {
  let fixture: ComponentFixture<CleansiaCookieConsentComponent>;

  function configure(platform: 'browser' | 'server'): void {
    TestBed.configureTestingModule({
      imports: [CleansiaCookieConsentComponent, TranslateModule.forRoot()],
      providers: [{ provide: PLATFORM_ID, useValue: platform }],
    });
  }

  function render(): HTMLElement {
    fixture = TestBed.createComponent(CleansiaCookieConsentComponent);
    fixture.componentRef.setInput('storageKey', STORAGE_KEY);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => localStorage.clear());

  it('shows itself to a visitor who has not seen it', () => {
    configure('browser');

    expect(render().querySelector('.cleansia-cookie-consent')).not.toBeNull();
  });

  it.each(['acknowledged', 'accepted', 'declined', 'custom'])(
    'stays hidden once anything was stored, including the old banner answer %s',
    (stored) => {
      configure('browser');
      localStorage.setItem(STORAGE_KEY, stored);

      expect(render().querySelector('.cleansia-cookie-consent')).toBeNull();
    }
  );

  it('remembers the dismissal and hides', () => {
    configure('browser');
    const element = render();

    (element.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(localStorage.getItem(STORAGE_KEY)).toBe('acknowledged');
    expect(element.querySelector('.cleansia-cookie-consent')).toBeNull();
  });

  it('offers one button and nothing to accept or refuse', () => {
    configure('browser');
    const element = render();

    expect(element.querySelectorAll('button').length).toBe(1);
    expect(element.querySelectorAll('input, p-toggleswitch').length).toBe(0);
  });

  it('renders nothing on the server, where no storage can say it was seen', () => {
    configure('server');

    expect(render().querySelector('.cleansia-cookie-consent')).toBeNull();
  });

  it.each(LOCALES)('the customer %s bundle carries only the notice copy', (locale) => {
    const bundle = JSON.parse(readFileSync(join(CUSTOMER_I18N_DIR, `${locale}.json`), 'utf8')) as {
      cookies: Record<string, unknown>;
    };

    expect(Object.keys(bundle.cookies).sort()).toEqual(NOTICE_KEYS);
    expect(NOTICE_KEYS.filter((key) => typeof bundle.cookies[key] !== 'string' || !bundle.cookies[key])).toEqual([]);
  });
});
