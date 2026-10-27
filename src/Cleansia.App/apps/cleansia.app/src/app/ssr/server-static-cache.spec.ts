/**
 * @jest-environment node
 * @jest-environment-options {"__Zone_disable_EventTarget": true}
 */
// The option is needed under a node environment; server-request-path.spec.ts says why.

import * as express from 'express';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'fs';
import { createServer, type Server } from 'http';
import type { AddressInfo } from 'net';
import { tmpdir } from 'os';
import { dirname, join, relative } from 'path';
import { staticCacheControl } from './static-cache-control';

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const SERVER = readFileSync(join(findSolutionDir(), 'Cleansia.App/apps/cleansia.app/server.ts'), 'utf8');
const IMMUTABLE = 'public, max-age=31536000, immutable';

/**
 * A file whose name carries the build's content hash never changes under that name, so a browser
 * keeps it without asking. Every other file keeps its address from build to build (the translation
 * files above all), so a browser asks before each use and its ETag turns the answer into a 304.
 */
describe('how long a browser keeps a file of the customer build', () => {
  it.each([
    'main-NNEZJWA5.js',
    'chunk-EQEPPNDR.js',
    'polyfills-5CFQRCPP.js',
    'styles-TSMX3RLF.css',
    'media/inter-latin-4AB7QZ2M.woff2',
  ])('keeps the hashed %s for a year without asking', (path) => {
    expect(staticCacheControl(path)).toBe(IMMUTABLE);
  });

  it.each([
    'assets/i18n/en.json',
    'robots.txt',
    'assets/logos/Logo.ico',
    'assets/images/mascot/mascot-dusting-tile.webp',
    'assets/videos/mascot-cleaning-in-progress.webm',
    'assets/fonts/inter.woff2',
    'assets/flags/4x3/cz.svg',
    'index.csr.html',
    'assets/i18n/main-NNEZJWA5.js',
  ])('revalidates %s on every use', (path) => {
    expect(staticCacheControl(path)).toBe('no-cache');
  });

  it('reads a path written with Windows separators the same way', () => {
    expect(staticCacheControl('media\\inter-latin-4AB7QZ2M.woff2')).toBe(IMMUTABLE);
    expect(staticCacheControl('assets\\i18n\\en.json')).toBe('no-cache');
  });
});

describe('the browser build mount in server.ts', () => {
  const mount = SERVER.slice(SERVER.indexOf('express.static(browserDistFolder'));
  const options = mount.slice(0, mount.indexOf('}),'));

  it('leaves the header to the rule instead of a lifetime of its own', () => {
    expect(options).toContain('cacheControl: false');
    expect(options).not.toContain('maxAge');
    expect(options).toContain(
      "res.setHeader('Cache-Control', staticCacheControl(relative(browserDistFolder, path)))",
    );
  });
});

describe('express.static under that rule', () => {
  let root: string;
  let server: Server;
  let base: string;

  beforeAll(async () => {
    root = mkdtempSync(join(tmpdir(), 'cleansia-static-'));
    mkdirSync(join(root, 'assets/i18n'), { recursive: true });
    writeFileSync(join(root, 'main-ABCDEFGH.js'), 'console.log(1);');
    writeFileSync(join(root, 'assets/i18n/en.json'), '{"auth":{"login":{"lead":"x"}}}');
    writeFileSync(join(root, 'robots.txt'), 'User-agent: *');

    const app = express();
    app.use(
      express.static(root, {
        index: false,
        redirect: false,
        cacheControl: false,
        setHeaders: (res, path) => {
          res.setHeader('Cache-Control', staticCacheControl(relative(root, path)));
        },
      }),
    );
    server = createServer(app);
    await new Promise<void>((resolve) => server.listen(0, resolve));
    base = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
  });

  afterAll(async () => {
    await new Promise<void>((resolve) => server.close(() => resolve()));
    rmSync(root, { recursive: true, force: true });
  });

  it('answers a hashed bundle as immutable for a year', async () => {
    const response = await fetch(`${base}/main-ABCDEFGH.js`);

    expect(response.status).toBe(200);
    expect(response.headers.get('cache-control')).toBe(IMMUTABLE);
  });

  it.each(['/assets/i18n/en.json', '/robots.txt'])('answers %s as no-cache with an ETag', async (path) => {
    const response = await fetch(`${base}${path}`);

    expect(response.status).toBe(200);
    expect(response.headers.get('cache-control')).toBe('no-cache');
    expect(response.headers.get('etag')).toBeTruthy();
  });

  it('answers a revalidation of an unchanged translation file with a 304', async () => {
    const first = await fetch(`${base}/assets/i18n/en.json`);
    const etag = first.headers.get('etag') ?? '';

    const again = await fetch(`${base}/assets/i18n/en.json`, {
      headers: { 'If-None-Match': etag, 'Cache-Control': 'max-age=0' },
    });

    expect(again.status).toBe(304);
  });
});
