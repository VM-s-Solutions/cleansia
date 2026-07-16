import { AngularNodeAppEngine, createNodeRequestHandler, isMainModule, writeResponseToNodeResponse } from '@angular/ssr/node';
import { ɵsetAngularAppEngineManifest } from '@angular/ssr';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import compression from 'compression';
import express from 'express';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');

const app = express();
app.use(compression());

let angularApp: AngularNodeAppEngine | undefined;
let manifestLoaded = false;

async function getAngularApp(): Promise<AngularNodeAppEngine> {
  if (!manifestLoaded) {
    const manifestPath = pathToFileURL(resolve(serverDistFolder, 'angular-app-engine-manifest.mjs')).href;
    const engineManifest = await import(manifestPath);
    // @angular/ssr >= 19.2.16 (SSRF fix) iterates manifest.allowedHosts
    // unconditionally, but @angular/build < 19.2.16 emits a manifest without
    // it — default the field so the engine doesn't crash on startup. Hosts
    // are authorized at runtime via the NG_ALLOWED_HOSTS env var.
    ɵsetAngularAppEngineManifest({ allowedHosts: [], ...engineManifest.default });
    manifestLoaded = true;
  }
  return (angularApp ??= new AngularNodeAppEngine());
}

app.get('/health', (_req, res) => {
  res.json({
    status: 'healthy',
    timestamp: new Date().toISOString(),
    uptime: process.uptime(),
    memory: process.memoryUsage(),
  });
});

/**
 * Same-origin Mapbox geocoding proxy.
 *
 * The Mapbox Geocoding REST API authenticates only via the `access_token` query
 * parameter (no `Authorization` header support), which would leak the token into
 * browser history / referrer / CDN+APM logs if the browser called Mapbox
 * directly. This proxy keeps the token OUT of the browser: it injects the
 * server-only token (process.env.MAPBOX_TOKEN) into the upstream call and never
 * logs the token-bearing URL. The browser sends only `q`/`country`/`types`/
 * `language`/`limit` to this same-origin path.
 */
// Mapbox forward geocoding v5: the query is in the PATH (…/mapbox.places/{q}.json),
// the rest are query params. The browser-facing service parses the v5 feature shape
// (feature.center / place_name / text / address / context[]), so the proxy must call
// v5 — v6 returns a different geometry/properties shape the parser would not read.
const MAPBOX_PLACES_BASE =
  'https://api.mapbox.com/geocoding/v5/mapbox.places';
const MAPBOX_PROXY_ALLOWED_PARAMS = [
  'country',
  'types',
  'language',
  'limit',
  'autocomplete',
] as const;

app.get('/api/mapbox/geocode', (req, res) => {
  const token = process.env['MAPBOX_TOKEN'] ?? '';
  if (!token) {
    // No server-side token provisioned: behave like "not configured".
    res.status(503).json({ features: [] });
    return;
  }

  const q = req.query['q'];
  if (typeof q !== 'string' || q.length === 0) {
    res.status(400).json({ features: [] });
    return;
  }

  const upstream = new URL(`${MAPBOX_PLACES_BASE}/${encodeURIComponent(q)}.json`);
  for (const key of MAPBOX_PROXY_ALLOWED_PARAMS) {
    const value = req.query[key];
    if (typeof value === 'string' && value.length > 0) {
      upstream.searchParams.set(key, value);
    }
  }
  // Token is injected here, server-side only — never logged below.
  upstream.searchParams.set('access_token', token);

  fetch(upstream, { headers: { Accept: 'application/json' } })
    .then(async (upstreamRes) => {
      const body = await upstreamRes.text();
      res
        .status(upstreamRes.ok ? 200 : upstreamRes.status)
        .type('application/json')
        .send(upstreamRes.ok ? body : JSON.stringify({ features: [] }));
    })
    .catch(() => {
      // Log without the token-bearing upstream URL.
      console.error('Mapbox geocoding proxy upstream request failed');
      res.status(502).json({ features: [] });
    });
});

app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

// Micro-cache for the anonymous landing page: '/' is identical for every
// visitor without a session (auth state is resolved client-side), and the
// SSR render costs ~250ms per request. 60s of staleness is invisible for a
// marketing page but turns TTFB into a static-file read.
// SSR renders in the Accept-Language language, so the cache is keyed by the
// same resolution — one entry per supported language, not one global page.
const LANDING_CACHE_TTL_MS = 60_000;
const LANDING_LANGUAGES = new Set(['cs', 'en', 'sk', 'uk', 'ru']);
const landingCache = new Map<string, { body: Buffer; headers: [string, string][]; expires: number }>();

function resolveLandingLanguage(acceptLanguage: string | undefined): string {
  for (const part of (acceptLanguage ?? '').split(',')) {
    const primary = part.split(';')[0]?.trim().split('-')[0]?.toLowerCase();
    if (primary && LANDING_LANGUAGES.has(primary)) {
      return primary;
    }
  }
  return 'en';
}

app.use('{*path}', (req, res, next) => {
  const cacheable = req.path === '/' && req.method === 'GET' && !req.headers.cookie;
  const cacheKey = resolveLandingLanguage(req.headers['accept-language']);

  const cached = cacheable ? landingCache.get(cacheKey) : undefined;
  if (cached && cached.expires > Date.now()) {
    res.status(200);
    for (const [key, value] of cached.headers) {
      res.setHeader(key, value);
    }
    res.send(cached.body);
    return;
  }

  getAngularApp()
    .then(async (engine) => {
      const response = await engine.handle(req);
      if (!response) {
        next();
        return;
      }
      if (cacheable && response.status === 200) {
        response.headers.set('vary', 'Accept-Language');
        const body = Buffer.from(await response.clone().arrayBuffer());
        // A transient SSR failure still responds 200 with the bare app shell;
        // caching that would serve the broken page to every visitor of this
        // language for the whole TTL. The landing page always contains the
        // hero section, so its absence marks a render to skip.
        if (body.includes('cl-hero')) {
          const headers: [string, string][] = [];
          response.headers.forEach((value, key) => {
            if (!['set-cookie', 'content-length'].includes(key.toLowerCase())) {
              headers.push([key, value]);
            }
          });
          landingCache.set(cacheKey, { body, headers, expires: Date.now() + LANDING_CACHE_TTL_MS });
        }
      }
      await writeResponseToNodeResponse(response, res);
    })
    .catch(next);
});

if (isMainModule(import.meta.url)) {
  const port = parseInt(process.env['PORT'] ?? '4000', 10);
  app.listen(port, () => {
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

export default createNodeRequestHandler(app);
