import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The partner lists share the admin lists' column rule: a figure, an amount, a count or a stamp
 * is right-aligned in tabular figures through the table's `numeric` flag, on a list and in a
 * detail section table alike. One flag, so the partner models read like the admin ones.
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

// A column whose id names a figure, an amount or a count, or ends in a stamp's suffix.
const FIGURE_COLUMN_ID = /(price|amount|total|count|pay|spots|orders?)$/i;
const STAMP_COLUMN_ID = /(On|At|Date|DateTime|From)$/;
const isNumericColumn = (id: string): boolean => FIGURE_COLUMN_ID.test(id) || STAMP_COLUMN_ID.test(id);

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, out);
    else out.push(path);
  }
  return out;
}

interface ColumnLiteral {
  file: string;
  id: string;
  body: string;
}

// Every `{ id: '…', … }` object literal in a file, with its body up to the matching brace; a table
// column is the one that also names its field and header.
function columnLiterals(file: string): ColumnLiteral[] {
  const source = readFileSync(file, 'utf8');
  const found: ColumnLiteral[] = [];
  const open = /\{\s*id:\s*'([^']+)'/g;
  let match: RegExpExecArray | null;
  while ((match = open.exec(source)) !== null) {
    let depth = 1;
    let end = match.index + 1;
    while (depth > 0 && end < source.length) {
      if (source[end] === '{') depth++;
      else if (source[end] === '}') depth--;
      end++;
    }
    found.push({
      file: relative(APP_DIR, file).replace(/\\/g, '/'),
      id: match[1],
      body: source.slice(match.index, end),
    });
  }
  return found;
}

describe('partner list pages', () => {
  const columns = walk(FEATURES_DIR)
    .filter((file) => /\.models\.ts$/.test(file) && !file.endsWith('.spec.ts'))
    .flatMap(columnLiterals)
    .filter(({ body }) => /\bfield:/.test(body) && /\bheader:/.test(body));

  it('right-align every figure, amount, count and stamp column through the numeric flag', () => {
    const offenders = columns
      .filter(({ id, body }) => isNumericColumn(id) && !/numeric:\s*true/.test(body))
      .map(({ file, id }) => `${file} → ${id}`)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('name the alignment one way, never through align right beside the flag', () => {
    const offenders = columns
      .filter(({ body }) => /align:\s*'right'/.test(body))
      .map(({ file, id }) => `${file} → ${id}`)
      .sort();

    expect(offenders).toEqual([]);
  });
});
