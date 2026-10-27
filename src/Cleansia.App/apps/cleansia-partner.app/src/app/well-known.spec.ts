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

const SOLUTION_DIR = findSolutionDir();
const APP = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-partner.app');
const DEPLOY = join(SOLUTION_DIR, '../.github/workflows/deploy-azure.yml');
const AASA = '/.well-known/apple-app-site-association';
const AASA_FILE = 'apple-app-site-association.json';

const read = (path: string): string => readFileSync(path, 'utf8');

/** The text of one workflow step, from its `- name:` line to the next one. */
function step(workflow: string, name: string): string {
  const start = workflow.indexOf(`- name: ${name}\n`);
  expect(start).toBeGreaterThanOrEqual(0);
  const next = workflow.indexOf('- name: ', start + 1);
  return workflow.slice(start, next < 0 ? undefined : next);
}

/**
 * iOS offers and saves a strong password for the partner app only when this host vouches for it:
 * the file must answer at /.well-known/apple-app-site-association with a 200, as JSON, with no redirect.
 */
describe('the partner host vouches for the partner iOS app', () => {
  it('names the partner app under webcredentials', () => {
    expect(JSON.parse(read(join(APP, 'src/well-known', AASA_FILE)))).toEqual({
      webcredentials: { apps: ['ST9QYZHG63.cz.cleansia.partner'] },
    });
  });

  it('is copied to /.well-known by the build', () => {
    const assets: unknown[] = JSON.parse(read(join(APP, 'project.json'))).targets.build.options.assets;

    expect(assets).toContainEqual({ glob: '**/*', input: 'apps/cleansia-partner.app/src/well-known', output: '/.well-known' });
  });

  // The Static Web App types a file by its extension: with a content-type route header on the bare
  // name the deployed host still answered application/octet-stream. So the path is rewritten, with no
  // redirect, to the file under the extension the JSON type is mapped to.
  it('is answered as JSON by the Static Web App, through a rewrite to the file under a .json name', () => {
    const config: {
      routes?: { route: string; rewrite?: string; redirect?: string; statusCode?: number; headers?: Record<string, string> }[];
      mimeTypes?: Record<string, string>;
    } = JSON.parse(read(join(APP, 'src/staticwebapp.config.json')));
    const route = (config.routes ?? []).find((r) => r.route === AASA);

    expect(route).toEqual({ route: AASA, rewrite: `/.well-known/${AASA_FILE}` });
    expect(config.mimeTypes?.['.json']).toBe('application/json');
    expect(existsSync(join(APP, 'src/well-known', AASA_FILE))).toBe(true);
    expect(existsSync(join(APP, 'src/well-known/apple-app-site-association'))).toBe(false);
  });

  it('reaches the deployed host, since the artifact upload drops dot-folders unless told to keep them', () => {
    expect(step(read(DEPLOY), 'Upload Partner App artifact')).toContain('include-hidden-files: true');
  });
});
