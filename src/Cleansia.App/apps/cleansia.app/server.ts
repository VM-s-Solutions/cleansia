import { AngularNodeAppEngine, createNodeRequestHandler, isMainModule, writeResponseToNodeResponse } from '@angular/ssr/node';
import { ɵsetAngularAppEngineManifest } from '@angular/ssr';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import express from 'express';

const serverDistFolder = dirname(fileURLToPath(import.meta.url));
const browserDistFolder = resolve(serverDistFolder, '../browser');

const app = express();

let angularApp: AngularNodeAppEngine | undefined;
let manifestLoaded = false;

async function getAngularApp(): Promise<AngularNodeAppEngine> {
  if (!manifestLoaded) {
    const manifestPath = pathToFileURL(resolve(serverDistFolder, 'angular-app-engine-manifest.mjs')).href;
    const engineManifest = await import(manifestPath);
    ɵsetAngularAppEngineManifest(engineManifest.default);
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

app.use('{*path}', (req, res, next) => {
  getAngularApp()
    .then((engine) => engine.handle(req))
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

if (isMainModule(import.meta.url)) {
  const port = parseInt(process.env['PORT'] ?? '4000', 10);
  app.listen(port, () => {
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

export default createNodeRequestHandler(app);
