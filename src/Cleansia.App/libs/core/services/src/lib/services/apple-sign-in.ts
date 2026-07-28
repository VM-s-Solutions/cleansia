import { InjectionToken } from '@angular/core';

/**
 * Sign in with Apple **Services ID** for the current deployment.
 *
 * NOT the iOS bundle id (`cz.cleansia.customer`) — that is the *native*
 * audience and the web host deliberately refuses it, so pasting it here makes
 * every browser sign-in fail closed with a generic error. The web value is the
 * Services ID created under the same primary App ID, e.g.
 * `cz.cleansia.customer.web`.
 *
 * Like the GSI client id this is a public identifier, not a secret: Apple gates
 * access on the domain registered against the Services ID, so it is safe in the
 * bundle but *deployment-specific*. Provide it from each app's `environment*.ts`
 * as `environment.appleClientId`.
 *
 * The factory default is empty, which every consumer must read as "Apple
 * sign-in is not configured for this deployment": do not load Apple's script,
 * do not call `init()`, and hide the button entirely rather than rendering one
 * that cannot work. Apple rejects `localhost` and any non-HTTPS return URL, so
 * local development is permanently in the empty case.
 */
export const APPLE_CLIENT_ID = new InjectionToken<string>('APPLE_CLIENT_ID', {
  factory: () => '',
});

/**
 * Apple's JS SDK. The locale segment only affects the branded button Apple can
 * render for you — we render our own — so it is pinned rather than translated.
 */
export const APPLE_ID_SCRIPT_URL =
  'https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/en_US/appleid.auth.js';

/**
 * Path half of `redirectURI`. Apple validates the full URL against the Return
 * URLs registered on the Services ID even in popup mode (where the result comes
 * back through the promise and the browser never navigates here), so this must
 * stay byte-identical to the portal entry.
 */
export const APPLE_REDIRECT_PATH = '/auth/apple/callback';

export interface AppleIdInitOptions {
  clientId: string;
  scope: string;
  redirectURI: string;
  /** The SHA-256 of the raw nonce — see {@link createAppleNonce}. */
  nonce: string;
  usePopup: boolean;
}

export interface AppleIdSignInResponse {
  authorization: { id_token: string; code: string; state?: string };
  /** Apple sends the name ONLY on the very first authorization for this app. */
  user?: { name?: { firstName?: string; lastName?: string }; email?: string };
}

export interface AppleIdApi {
  auth: {
    init(options: AppleIdInitOptions): void;
    signIn(): Promise<AppleIdSignInResponse>;
  };
}

/** Browser-only — callers must already have established `isPlatformBrowser`. */
export function getAppleIdApi(): AppleIdApi | undefined {
  return (window as Window & { AppleID?: AppleIdApi }).AppleID;
}

export interface AppleNonce {
  /** POSTed to our API as `rawNonce`; never handed to Apple. */
  raw: string;
  /** Handed to Apple as `nonce`; never POSTed to our API. */
  hashed: string;
}

// Mirrors Nonce.swift's charset — 65 characters, which does NOT divide 256, so
// `byte % 65` would map bytes 0-60 four times and 61-64 only three. Reject the
// short tail instead, exactly as Nonce.swift does, and the draw stays uniform.
const NONCE_ALPHABET =
  '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-._';

/** Largest multiple of the alphabet that fits in a byte; above it we redraw. */
const NONCE_UNBIASED_CEILING =
  Math.floor(256 / NONCE_ALPHABET.length) * NONCE_ALPHABET.length;

const NONCE_LENGTH = 32;

/**
 * Anti-replay nonce for one Apple sign-in attempt.
 *
 * Apple echoes whatever we pass to `AppleID.auth.init({ nonce })` verbatim into
 * the identity token's `nonce` claim; `AppleTokenVerifier` then recomputes
 * lowercase-hex SHA-256 over the raw nonce we POST and requires an exact match.
 * So the HASH goes to Apple and the RAW goes to our API — swapping the two, or
 * hex-encoding in uppercase, rejects 100% of sign-ins with a generic error.
 *
 * Browser-only: `crypto.subtle` exists solely in a secure context, so callers
 * must guard on `isPlatformBrowser` before awaiting this.
 */
export async function createAppleNonce(): Promise<AppleNonce> {
  let raw = '';
  while (raw.length < NONCE_LENGTH) {
    for (const byte of crypto.getRandomValues(new Uint8Array(NONCE_LENGTH))) {
      if (byte < NONCE_UNBIASED_CEILING && raw.length < NONCE_LENGTH) {
        raw += NONCE_ALPHABET[byte % NONCE_ALPHABET.length];
      }
    }
  }

  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(raw));
  const hashed = Array.from(new Uint8Array(digest), (byte) =>
    byte.toString(16).padStart(2, '0')
  ).join('');

  return { raw, hashed };
}

/**
 * True when the rejection is the visitor closing Apple's popup rather than a
 * genuine failure — the difference between "say nothing" and "show an error".
 */
export function isAppleSignInCancelled(error: unknown): boolean {
  const code = (error as { error?: string } | null)?.error;
  return (
    code === 'popup_closed_by_user' ||
    code === 'user_cancelled_authorize' ||
    code === 'user_trigger_new_signin_flow'
  );
}
