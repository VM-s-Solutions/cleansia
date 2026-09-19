import { InjectionToken } from '@angular/core';

/**
 * Google Identity Services OAuth **client id** for the current deployment.
 *
 * Unlike the Mapbox access token, this is a public identifier, not a secret —
 * GSI requires it in the browser, and Google enforces access by matching the
 * page origin against the client's "Authorized JavaScript origins" list. It is
 * therefore safe to ship in the bundle, but it is *deployment-specific*: a
 * client id whose origin list does not contain the host serving the page makes
 * GSI fail with `403 origin not allowed` and renders a dead button.
 *
 * Provide it from each app's `environment*.ts` as `environment.googleClientId`.
 * The factory default is empty, which every consumer must read as
 * "Google sign-in is not configured for this deployment": do not load the GSI
 * script, do not call `initialize()`, and hide the button entirely rather than
 * rendering one that cannot work.
 */
export const GOOGLE_CLIENT_ID = new InjectionToken<string>('GOOGLE_CLIENT_ID', {
  factory: () => '',
});

export interface GoogleIdCredentialResponse {
  credential: string;
}

export interface GoogleIdInitializeOptions {
  client_id: string;
  callback: (response: GoogleIdCredentialResponse) => void;
}

/** GSI's `width` is an integer pixel count, max 400 — a percentage string is rejected. */
export interface GoogleIdButtonOptions {
  theme: 'outline' | 'filled_blue' | 'filled_black';
  size: 'large' | 'medium' | 'small';
  width: number;
  text: 'signin_with' | 'signup_with' | 'continue_with' | 'signin';
  shape: 'rectangular' | 'pill' | 'circle' | 'square';
}

/** `google.accounts.id` — the identity half of the GSI script. */
export interface GoogleIdApi {
  initialize(options: GoogleIdInitializeOptions): void;
  renderButton(parent: HTMLElement, options: GoogleIdButtonOptions): void;
}

/**
 * Browser-only — callers must already have established `isPlatformBrowser`. Another Google
 * script can own `window.google` before identity has attached, so the whole path is optional.
 */
export function getGoogleIdApi(): GoogleIdApi | undefined {
  return (window as Window & { google?: { accounts?: { id?: GoogleIdApi } } }).google?.accounts
    ?.id;
}
