/**
 * @jest-environment node
 * @jest-environment-options {"__Zone_disable_EventTarget": true}
 */
// The option is needed under a node environment; server-request-path.spec.ts says why.

import * as express from 'express';
import { existsSync, readFileSync } from 'fs';
import { createServer, type Server } from 'http';
import type { AddressInfo } from 'net';
import { basename, dirname, join } from 'path';

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

const SOLUTION_DIR = findSolutionDir();
const APP = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia.app');
const DEPLOY = join(SOLUTION_DIR, '../.github/workflows/deploy-azure.yml');

const read = (path: string): string => readFileSync(path, 'utf8');

/** The text of one workflow step, from its `- name:` line to the next one. */
function step(workflow: string, name: string): string {
  const start = workflow.indexOf(`- name: ${name}\n`);
  expect(start).toBeGreaterThanOrEqual(0);
  const next = workflow.indexOf('- name: ', start + 1);
  return workflow.slice(start, next < 0 ? undefined : next);
}

/**
 * iOS offers and saves a strong password for the customer app only when this host vouches for it:
 * the file must answer at /.well-known/apple-app-site-association with a 200, as JSON, with no redirect.
 */
describe('the customer host vouches for the customer iOS app', () => {
  it('names the customer app under webcredentials', () => {
    expect(JSON.parse(read(join(APP, 'src/root-assets/well-known/apple-app-site-association')))).toEqual({
      webcredentials: { apps: ['ST9QYZHG63.cz.cleansia.customer'] },
    });
  });

  it('is copied to /.well-known by the build', () => {
    const assets: unknown[] = JSON.parse(read(join(APP, 'project.json'))).targets.build.options.assets;

    expect(assets).toContainEqual(
      expect.objectContaining({ glob: '**/*', input: 'apps/cleansia.app/src/root-assets/well-known', output: '/.well-known' }),
    );
  });

  it('is answered as JSON by the /.well-known mount, since the file has no extension to type it by', () => {
    const server = read(join(APP, 'server.ts'));
    const mount = server.slice(server.indexOf("'/.well-known',"), server.indexOf('express.static(browserDistFolder'));

    expect(mount).toContain("basename(path) === 'apple-app-site-association'");
    expect(mount).toContain("res.setHeader('Content-Type', 'application/json')");
    expect(mount).toContain('redirect: false');
  });

  it('is answered with a 200 as JSON by express.static under that mount, the file served from its source folder', async () => {
    const app = express();
    app.use(
      '/.well-known',
      express.static(join(APP, 'src/root-assets/well-known'), {
        dotfiles: 'allow',
        index: false,
        redirect: false,
        setHeaders: (res, path) => {
          if (basename(path) === 'apple-app-site-association') {
            res.setHeader('Content-Type', 'application/json');
          }
        },
      }),
    );
    const server: Server = createServer(app);
    await new Promise<void>((resolve) => server.listen(0, resolve));

    try {
      const { port } = server.address() as AddressInfo;
      const response = await fetch(`http://127.0.0.1:${port}/.well-known/apple-app-site-association`, {
        redirect: 'manual',
      });

      expect(response.status).toBe(200);
      expect(response.headers.get('content-type')).toMatch(/^application\/json(;|$)/);
      expect(await response.json()).toEqual({ webcredentials: { apps: ['ST9QYZHG63.cz.cleansia.customer'] } });
    } finally {
      await new Promise<void>((resolve) => server.close(() => resolve()));
    }
  });

  it('reaches the deployed host, since the artifact upload drops dot-folders unless told to keep them', () => {
    expect(step(read(DEPLOY), 'Upload Customer App artifact (SSR)')).toContain('include-hidden-files: true');
  });
});
