import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // Static pages — prerendered at build time (no API calls needed)
  { path: 'terms', renderMode: RenderMode.Prerender },
  { path: 'privacy', renderMode: RenderMode.Prerender },
  { path: 'not-found', renderMode: RenderMode.Server },

  // SSR pages — rendered on request (need API data for SEO)
  { path: '', renderMode: RenderMode.Server },
  { path: 'services', renderMode: RenderMode.Server },

  // Dynamic / auth-gated pages — client-side only
  { path: '**', renderMode: RenderMode.Client },
];
