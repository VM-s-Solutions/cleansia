import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // SSR pages — rendered on request (need API data for SEO).
  // The legal pages are Server, not Prerender: prerendering needs the builder's
  // `outputMode`, without which the engine answers a Prerender route with a 404
  // because no document was emitted for it at build time.
  { path: 'terms', renderMode: RenderMode.Server },
  { path: 'privacy', renderMode: RenderMode.Server },
  { path: 'not-found', renderMode: RenderMode.Server },
  { path: '', renderMode: RenderMode.Server },
  { path: 'services', renderMode: RenderMode.Server },

  // Dynamic / auth-gated pages — client-side only
  { path: '**', renderMode: RenderMode.Client },
];
