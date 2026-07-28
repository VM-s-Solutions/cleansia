import { webcrypto } from 'node:crypto';
import { createAppleNonce, isAppleSignInCancelled } from './apple-sign-in';

// jsdom ships getRandomValues but no SubtleCrypto; the browser always has both
// (crypto.subtle exists only in a secure context, which is why the callers are
// guarded by isPlatformBrowser).
beforeAll(() => {
  Object.defineProperty(globalThis, 'crypto', { value: webcrypto, configurable: true });
});

describe('createAppleNonce', () => {
  it('hashes the raw nonce to lowercase hex SHA-256 — the exact form AppleTokenVerifier recomputes', async () => {
    const { raw, hashed } = await createAppleNonce();

    const expected = Buffer.from(
      await webcrypto.subtle.digest('SHA-256', new TextEncoder().encode(raw))
    ).toString('hex');

    expect(hashed).toBe(expected);
    expect(hashed).toMatch(/^[0-9a-f]{64}$/);
    // Uppercase hex is the silent-failure mode: Apple echoes it back happily
    // and the server rejects every token with a generic error.
    expect(hashed).toBe(hashed.toLowerCase());
  });

  it('draws the raw nonce from Nonce.swift’s alphabet and never returns the hash as raw', async () => {
    const { raw, hashed } = await createAppleNonce();

    expect(raw).toHaveLength(32);
    expect(raw).toMatch(/^[0-9A-Za-z\-._]{32}$/);
    expect(raw).not.toBe(hashed);
  });

  it('is fresh per call', async () => {
    const first = await createAppleNonce();
    const second = await createAppleNonce();

    expect(first.raw).not.toBe(second.raw);
  });
});

describe('isAppleSignInCancelled', () => {
  it('treats a closed popup as a cancellation, not a failure', () => {
    expect(isAppleSignInCancelled({ error: 'popup_closed_by_user' })).toBe(true);
    expect(isAppleSignInCancelled({ error: 'user_cancelled_authorize' })).toBe(true);
  });

  it('treats everything else as a failure worth reporting', () => {
    expect(isAppleSignInCancelled({ error: 'invalid_client' })).toBe(false);
    expect(isAppleSignInCancelled(new Error('script blocked'))).toBe(false);
    expect(isAppleSignInCancelled(null)).toBe(false);
  });
});
