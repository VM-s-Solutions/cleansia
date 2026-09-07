import { DestroyRef, inject, WritableSignal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';

/**
 * Clear a "submitting" flag when the browser restores this page from the
 * back/forward cache.
 *
 * A page that hands the browser to an external checkout deliberately leaves its
 * in-flight flag SET: the tab is navigating away, and clearing it would
 * re-enable the button for the moment before the redirect lands — long enough
 * for a second click to open a second payment session.
 *
 * Pressing Back then brings the page back from the bfcache with the JavaScript
 * heap exactly as it was, flag and all. The button stays disabled and its guard
 * keeps returning early, so the page looks alive and does nothing — which is
 * indistinguishable, to the person pressing it, from a broken button. A normal
 * reload has never had this problem because it rebuilds the state from scratch;
 * only the restore path needs the reset.
 *
 * `pageshow` with `persisted` is the only event that reports the restore.
 * `visibilitychange` fires for tab switches too, and would clear a flag that is
 * legitimately set.
 */
export function clearOnBackForwardRestore(...flags: WritableSignal<boolean>[]): void {
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  if (!isBrowser) return;

  const onPageShow = (event: PageTransitionEvent): void => {
    if (!event.persisted) return;
    for (const flag of flags) flag.set(false);
  };

  window.addEventListener('pageshow', onPageShow);
  inject(DestroyRef).onDestroy(() => window.removeEventListener('pageshow', onPageShow));
}
