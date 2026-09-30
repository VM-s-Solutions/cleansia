import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

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

const SERVER = readFileSync(
  join(findSolutionDir(), 'Cleansia.App/apps/cleansia.app/server.ts'),
  'utf8'
);

// The value column is the literal as written in server.ts, quotes included.
const HEADERS: [string, string][] = [
  ['X-Frame-Options', "'DENY'"],
  ['Content-Security-Policy', `"frame-ancestors 'none'"`],
  ['X-Content-Type-Options', "'nosniff'"],
  ['Referrer-Policy', "'strict-origin-when-cross-origin'"],
  ['Strict-Transport-Security', "'max-age=31536000'"],
];

function indexOf(fragment: string): number {
  const index = SERVER.indexOf(fragment);
  expect(index).toBeGreaterThanOrEqual(0);
  return index;
}

describe('customer SSR security headers', () => {
  it.each(HEADERS)('sets %s to %s', (name, value) => {
    expect(SERVER).toContain(`res.setHeader('${name}', ${value});`);
  });

  it.each([
    ['the health probe', "app.get('/health'"],
    ['the static files', 'express.static('],
    ['the landing-page micro-cache', 'landingCache.get('],
  ])('are set before %s can answer', (_what, fragment) => {
    const headers = indexOf("res.setHeader('X-Frame-Options'");

    expect(headers).toBeLessThan(indexOf(fragment));
  });
});
