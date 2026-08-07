import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { compile } from 'sass';

const APP_NAME = 'cleansia.app';

const BRAND_HEADING_FAMILY = 'poppins';

const CYRILLIC_CAPABLE_FAMILIES = new Set(['nunito']);

const CSS_GENERIC_FAMILIES = new Set([
  'serif',
  'sans-serif',
  'monospace',
  'cursive',
  'fantasy',
  'system-ui',
  'ui-serif',
  'ui-sans-serif',
  'ui-monospace',
  'ui-rounded',
  'math',
  'emoji',
  'fangsong',
]);

interface FontFamilyDeclaration {
  stylesheet: string;
  declaration: string;
  families: string[];
}

interface ProjectConfiguration {
  targets: { build: { options: { styles: string[] } } };
}

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

const FRONTEND_DIR = join(findSolutionDir(), 'Cleansia.App');
const APP_DIR = join(FRONTEND_DIR, 'apps', APP_NAME);

function buildStylesheets(): string[] {
  const project = JSON.parse(
    readFileSync(join(APP_DIR, 'project.json'), 'utf8')
  ) as ProjectConfiguration;
  return project.targets.build.options.styles;
}

function splitFamilies(value: string): string[] {
  return value
    .replace(/!important/g, '')
    .split(',')
    .map((family) =>
      family
        .trim()
        .replace(/^['"]|['"]$/g, '')
        .toLowerCase()
    )
    .filter((family) => family.length > 0);
}

function collectFontFamilyDeclarations(): FontFamilyDeclaration[] {
  const declarations: FontFamilyDeclaration[] = [];
  for (const stylesheet of buildStylesheets()) {
    const css = compile(join(FRONTEND_DIR, stylesheet), {
      quietDeps: true,
    }).css;
    for (const match of css.match(/font-family\s*:[^;}]+/g) ?? []) {
      const value = match.slice(match.indexOf(':') + 1).trim();
      if (value.includes('var(')) continue;
      declarations.push({
        stylesheet,
        declaration: `font-family: ${value}`,
        families: splitFamilies(value),
      });
    }
  }
  return declarations;
}

function loadedWebFontFamilies(): Set<string> {
  const html = readFileSync(join(APP_DIR, 'src', 'index.html'), 'utf8');
  const families = new Set<string>();
  for (const request of html.match(/fonts\.googleapis\.com\/css2\?[^"']+/g) ??
    []) {
    for (const param of request.match(/family=([^&"']+)/g) ?? []) {
      families.add(
        decodeURIComponent(param.slice('family='.length))
          .split(':')[0]
          .replace(/\+/g, ' ')
          .toLowerCase()
      );
    }
  }
  return families;
}

function unique(values: string[]): string[] {
  return [...new Set(values)];
}

describe(`font stack — Cyrillic-capable fallback (${APP_NAME})`, () => {
  const declarations = collectFontFamilyDeclarations();
  const brandDeclarations = declarations.filter((entry) =>
    entry.families.includes(BRAND_HEADING_FAMILY)
  );

  it('compiles the build stylesheets and finds brand-face declarations', () => {
    expect(buildStylesheets().length).toBeGreaterThan(0);
    expect(declarations.length).toBeGreaterThan(0);
    expect(brandDeclarations.length).toBeGreaterThan(0);
  });

  it('names a Cyrillic-capable family in every brand-face declaration', () => {
    const uncovered = brandDeclarations.filter(
      (entry) =>
        !entry.families.some((family) => CYRILLIC_CAPABLE_FAMILIES.has(family))
    );
    expect(unique(uncovered.map((entry) => entry.declaration))).toEqual([]);
  });

  it('keeps the brand face first so Latin rendering is unchanged', () => {
    const displaced = brandDeclarations.filter(
      (entry) => entry.families[0] !== BRAND_HEADING_FAMILY
    );
    expect(unique(displaced.map((entry) => entry.declaration))).toEqual([]);
  });

  it('orders the Cyrillic-capable family ahead of any generic family', () => {
    const misordered = brandDeclarations.filter((entry) => {
      const generic = entry.families.findIndex((family) =>
        CSS_GENERIC_FAMILIES.has(family)
      );
      const cyrillic = entry.families.findIndex((family) =>
        CYRILLIC_CAPABLE_FAMILIES.has(family)
      );
      return generic !== -1 && cyrillic > generic;
    });
    expect(unique(misordered.map((entry) => entry.declaration))).toEqual([]);
  });

  it('requests every Cyrillic-capable fallback the stylesheets rely on', () => {
    const loaded = loadedWebFontFamilies();
    const relied = unique(
      brandDeclarations.flatMap((entry) =>
        entry.families.filter((family) => CYRILLIC_CAPABLE_FAMILIES.has(family))
      )
    );
    expect(loaded.size).toBeGreaterThan(0);
    expect(relied.length).toBeGreaterThan(0);
    expect(relied.filter((family) => !loaded.has(family))).toEqual([]);
  });
});
