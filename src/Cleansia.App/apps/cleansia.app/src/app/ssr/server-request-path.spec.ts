/**
 * @jest-environment node
 * @jest-environment-options {"__Zone_disable_EventTarget": true}
 */
// The option above is not decoration. `setupFilesAfterEnv` loads jest-preset-angular's zone setup
// into EVERY suite in this project, this one included, and zone.js patches `EventTarget`
// unconditionally. Under a NODE environment that EventTarget is Node's real one, so zone also
// patches `worker_threads` MessagePort — and then reads `.type` off esbuild's raw
// `{type:'upAndRunning'}` bootstrap message, which carries no zone task:
//   TypeError: Cannot read properties of undefined (reading 'false')
// It only bites on a cold cache, when the transformer actually spawns that worker, which is why it
// failed once in CI and passed on every warm rerun. The flag skips zone's EventTarget patch only.

import * as express from 'express';
import { existsSync, readFileSync } from 'fs';
import { createServer, type Server } from 'http';
import type { AddressInfo } from 'net';
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

const SERVER_TS = join(
  findSolutionDir(),
  'Cleansia.App/apps/cleansia.app/server.ts'
);

async function seenPathFor(
  mount: (app: express.Express, handler: express.RequestHandler) => void,
  requested: string
): Promise<string> {
  const app = express();
  let seen = '';
  mount(app, (req, res) => {
    seen = req.url;
    res.end();
  });

  const server: Server = createServer(app);
  await new Promise<void>((resolve) => server.listen(0, resolve));
  const { port } = server.address() as AddressInfo;
  await fetch(`http://127.0.0.1:${port}${requested}`);
  await new Promise<void>((resolve) => server.close(() => resolve()));

  return seen;
}

describe('customer SSR request path', () => {
  it('is stripped when middleware is mounted on a wildcard pattern', async () => {
    const seen = await seenPathFor(
      (app, handler) => app.use('{*path}', handler),
      '/terms'
    );

    expect(seen).toBe('/');
  });

  it('survives when middleware is mounted without a path', async () => {
    const seen = await seenPathFor((app, handler) => app.use(handler), '/terms');

    expect(seen).toBe('/terms');
  });

  it('is not mounted on a wildcard pattern in server.ts', () => {
    expect(readFileSync(SERVER_TS, 'utf8')).not.toContain('{*path}');
  });
});
