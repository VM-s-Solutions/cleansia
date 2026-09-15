import { readPreferredLanguageCookie, resolveRequestLanguage } from './translation-loader.service';

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
