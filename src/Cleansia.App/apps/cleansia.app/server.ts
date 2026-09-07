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
    // it — default the field so the engine doesn't crash on startup.
    //
    // The hosts themselves come from NG_ALLOWED_HOSTS, which the framework reads
    // itself (node.mjs getArrayFromEnv) — do NOT add code here to do it. That
    // env var, and NG_TRUST_PROXY_HEADERS beside it, are set on the SSR App
    // Service in deploy/bicep/main.bicep. Until 2026-09-06 they were set nowhere
    // and this comment was the only mention of either in the repository, so every
    // production request fell back to client-side rendering with a 200. → T-0681
    //
    // An empty list still means CSR-for-everything, which is what a LOCAL run of
    // the built server does unless you export NG_ALLOWED_HOSTS=localhost.
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

// Domain-verification files (Apple's domain association today; apple-app-site-association
// and assetlinks.json later). These need their own mount because the general static
// handler below sees the request path `/.well-known/…`, and `send` defaults to
// dotfiles: 'ignore' — it rejects any path segment starting with a dot. The request then
// falls through to the SSR catch-all and answers the Angular HTML page with a 200, which
// the verifying party reports as "invalid file" rather than as a 404. Mounting at the
// prefix strips the dotted segment; `dotfiles: 'allow'` keeps nested dot-paths working
// without opening up the rest of the browser bundle.
app.use(
  '/.well-known',
  express.static(resolve(browserDistFolder, '.well-known'), {
    dotfiles: 'allow',
    index: false,
    redirect: false,
  }),
);

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

// Path-LESS on purpose. A wildcard path pattern here matches the same requests
// but makes Express strip the matched segment from req.url, so every deep link
// reached the render engine as '/' and was answered with the landing page.
app.use((req, res, next) => {
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
        // language for the whole TTL.
        //
        // The test is the EMPTY app-root, not the presence of the hero. It was
        // `body.includes('cl-hero')`, and the shell passes that: its inlined
        // critical CSS declares the custom property `--cl-hero-1`, so the
        // substring is there in all 12,182 bytes of a render that produced
        // nothing. The guard was inert on exactly the input it was written for.
        // A real render always fills app-root; a fallback never does.
        if (!body.includes('<app-root></app-root>')) {
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
