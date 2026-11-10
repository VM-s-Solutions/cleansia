import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { disputeReasonLabelKey } from './disputes-management.models';

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

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'];
const bundle = (locale: string): Record<string, unknown> =>
  JSON.parse(
    readFileSync(join(findSolutionDir(), `Cleansia.App/apps/cleansia-admin.app/src/assets/i18n/${locale}.json`), 'utf-8')
  );
const lookup = (tree: Record<string, unknown>, key: string): unknown =>
  key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], tree);

describe('disputes management models', () => {
  it('has a label for every dispute reason, and the unknown one, in all five locales', () => {
    const keys = [1, 2, 3, 4, 5, 6, 7, 8].map(disputeReasonLabelKey);
    keys.push(disputeReasonLabelKey(undefined));
    const missing = LOCALES.flatMap((locale) => {
      const tree = bundle(locale);
      return keys.filter((key) => typeof lookup(tree, key) !== 'string').map((key) => `${locale}: ${key}`);
    });

    expect(new Set(keys).size).toBe(9);
    expect(missing).toEqual([]);
  });

  it('reads an unknown reason as unknown', () => {
    expect(disputeReasonLabelKey(99)).toBe('enums.dispute_reason.unknown');
  });
});
