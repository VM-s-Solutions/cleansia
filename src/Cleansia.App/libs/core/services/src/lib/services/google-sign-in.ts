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
