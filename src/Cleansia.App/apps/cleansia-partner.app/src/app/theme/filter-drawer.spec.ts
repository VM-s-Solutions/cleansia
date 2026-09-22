import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The partner lists filter through the admin lists' drawer: a list with a filter form mounts
 * `<cleansia-filter-drawer>` and `<cleansia-filter-chips>` on the facade's `FilterDrawerState`;
 * the state helper owns opening, closing, resetting, chip removal and the debounced apply, so no
 * component or facade re-declares them and no page stylesheet draws a drawer. Each rule pins a
 * copy that used to live on one list.
 */

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

const APP_DIR = join(findSolutionDir(), 'Cleansia.App');
const FEATURES_DIR = join(APP_DIR, 'libs/cleansia-partner-features');
const PARTNER_PAGES_DIR = join(APP_DIR, 'libs/shared/assets/src/styles/pages/cleansia-partner');

const DRAWER_STATE_MEMBERS =
  /\b(openFilterDrawer|closeFilterDrawer|resetFilters|clearAllFilters|removeFilterChip|hasActiveFilters|activeFilterCount|activeFilterChips)\b/;

function walk(dir: string, pattern: RegExp, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, pattern, out);
    else if (pattern.test(name)) out.push(path);
  }
  return out;
}

const rel = (file: string) => relative(APP_DIR, file).replace(/\\/g, '/');
const read = (file: string) => readFileSync(file, 'utf8');

const templates = walk(FEATURES_DIR, /\.html$/);
const sources = walk(FEATURES_DIR, /(?<!\.spec)\.ts$/);
const pageStylesheets = readdirSync(PARTNER_PAGES_DIR)
  .filter((name) => name.endsWith('.scss'))
  .map((name) => join(PARTNER_PAGES_DIR, name));

function offenders(pattern: RegExp, files: string[]): string[] {
  return files
    .filter((file) => pattern.test(read(file)))
    .map(rel)
    .sort();
}

describe('partner filter drawer', () => {
  it('holds every filter form, with the chip row beside it, on the facade state', () => {
    const missing = templates
      .filter((file) => /\bfilterForm\b/.test(read(file)))
      .filter((file) => {
        const html = read(file);
        return (
          !/<cleansia-filter-drawer \[state\]="facade\.filters">/.test(html) ||
          !/<cleansia-filter-chips \[state\]="facade\.filters" \/>/.test(html)
        );
      })
      .map(rel)
      .sort();

    expect(missing).toEqual([]);
  });

  it('is built by the facade, never by a component', () => {
    expect(offenders(/new FilterDrawerState\b/, sources.filter((file) => file.endsWith('.component.ts')))).toEqual([]);
  });

  it('owns its open, close, reset, chips and apply — no feature re-declares them or subscribes to the form itself', () => {
    expect(offenders(DRAWER_STATE_MEMBERS, [...sources, ...templates])).toEqual([]);
    expect(offenders(/filterForm\.valueChanges/, sources)).toEqual([]);
  });

  it('is drawn once, in the shared component — no page-local drawer markup or stylesheet', () => {
    expect(offenders(/class="filter-drawer|class="filter-chips|class="filter-header/, templates)).toEqual([]);
    expect(offenders(/\.filter-drawer\b|\.filter-panel\b|\.filter-chips\b/, pageStylesheets).map((file) => file.split('/').pop())).toEqual([]);
  });
});
