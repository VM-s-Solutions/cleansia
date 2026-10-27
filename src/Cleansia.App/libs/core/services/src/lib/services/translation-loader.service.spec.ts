import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TransferState } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  i18nStateKey,
  JsonTranslationLoader,
  readPreferredLanguageCookie,
  resolveRequestLanguage,
} from './translation-loader.service';

/**
 * The server renders in the language the preference cookie names, and the browser must resolve the
 * SAME language from the same cookie before it looks anywhere else — a localStorage value the cookie
 * no longer agrees with would hydrate the server's page into another language.
 */
describe('readPreferredLanguageCookie', () => {
  it('reads a supported language off the cookie header', () => {
    expect(readPreferredLanguageCookie('foo=1; preferred_language=cs; bar=2')).toBe('cs');
  });

  it('lowercases a region-tagged value to its language', () => {
    expect(readPreferredLanguageCookie('preferred_language=SK-sk')).toBe('sk');
  });

  it('answers null for an unsupported language and for no cookie at all', () => {
    expect(readPreferredLanguageCookie('preferred_language=de')).toBeNull();
    expect(readPreferredLanguageCookie(null)).toBeNull();
    expect(readPreferredLanguageCookie('')).toBeNull();
  });
});

describe('resolveRequestLanguage', () => {
  it('prefers the cookie over Accept-Language', () => {
    expect(resolveRequestLanguage('preferred_language=uk', 'cs-CZ,cs;q=0.9')).toBe('uk');
  });

  it('falls back to the first supported Accept-Language, then English', () => {
    expect(resolveRequestLanguage(null, 'de-DE,de;q=0.9,sk;q=0.8')).toBe('sk');
    expect(resolveRequestLanguage(null, 'de-DE')).toBe('en');
  });
});

describe('the browser resolves the language the server rendered with', () => {
  const originalCookie = Object.getOwnPropertyDescriptor(Document.prototype, 'cookie');

  afterEach(() => {
    if (originalCookie) Object.defineProperty(Document.prototype, 'cookie', originalCookie);
    localStorage.clear();
  });

  it('reads the cookie before localStorage, so a stale localStorage value cannot repaint the page', () => {
    Object.defineProperty(Document.prototype, 'cookie', {
      configurable: true,
      get: () => 'preferred_language=cs',
      set: () => undefined,
    });
    localStorage.setItem('preferred_language', 'uk');

    expect(readPreferredLanguageCookie(document.cookie) ?? localStorage.getItem('preferred_language')).toBe('cs');
  });

  it('still honours localStorage when no cookie is set (the pre-cookie legacy store)', () => {
    Object.defineProperty(Document.prototype, 'cookie', {
      configurable: true,
      get: () => '',
      set: () => undefined,
    });
    localStorage.setItem('preferred_language', 'uk');

    expect(readPreferredLanguageCookie(document.cookie) ?? localStorage.getItem('preferred_language')).toBe('uk');
  });
});

/**
 * The dictionary keeps one address for every build, so a copy a browser holds is only safe to use once
 * the server has said it is still current: a stale one renders every newer key as the key itself.
 */
describe('JsonTranslationLoader in the browser', () => {
  let http: HttpTestingController;

  const createLoader = (): JsonTranslationLoader =>
    TestBed.runInInjectionContext(() => new JsonTranslationLoader(TestBed.inject(HttpClient), true));

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks the server to revalidate any copy the browser holds before using it', () => {
    let received: Record<string, unknown> | undefined;
    createLoader().getTranslation('en').subscribe((dictionary) => (received = dictionary));

    const request = http.expectOne('/assets/i18n/en.json');
    expect(request.request.cache).toBe('no-cache');
    request.flush({ auth: { login: { lead: 'Sign in to see your bookings' } } });

    expect(received).toEqual({ auth: { login: { lead: 'Sign in to see your bookings' } } });
  });

  it('makes no request for the language the server render handed over', () => {
    TestBed.inject(TransferState).set(i18nStateKey('cs'), { nav: { orders: 'Objednávky' } });
    let received: Record<string, unknown> | undefined;

    createLoader().getTranslation('cs').subscribe((dictionary) => (received = dictionary));

    http.expectNone('/assets/i18n/cs.json');
    expect(received).toEqual({ nav: { orders: 'Objednávky' } });
  });
});
