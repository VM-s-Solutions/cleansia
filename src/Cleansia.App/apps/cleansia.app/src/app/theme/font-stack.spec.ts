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

interface CompiledStylesheet {
  stylesheet: string;
  css: string;
}

interface AssetGlob {
  glob: string;
  input: string;
  output: string;
}

interface ProjectConfiguration {
  targets: { build: { options: { styles: string[]; assets: (string | AssetGlob)[] } } };
}

interface FontFace {
  family: string;
  urls: string[];
}

const SELF_HOSTED_FONT_PATH = '/assets/fonts/';

const GOOGLE_FONT_HOSTS = /fonts\.googleapis\.com|fonts\.gstatic\.com/;

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
// The build resolves bare package paths in the stylesheets; the plain compiler needs telling where.
const NODE_MODULES_DIR = join(FRONTEND_DIR, 'node_modules');

function buildOptions(): ProjectConfiguration['targets']['build']['options'] {
  const project = JSON.parse(
    readFileSync(join(APP_DIR, 'project.json'), 'utf8')
  ) as ProjectConfiguration;
  return project.targets.build.options;
}

function buildStylesheets(): string[] {
  return buildOptions().styles;
}

function compiledStylesheets(): CompiledStylesheet[] {
  return buildStylesheets().map((stylesheet) => ({
    stylesheet,
    css: compile(join(FRONTEND_DIR, stylesheet), { quietDeps: true, loadPaths: [NODE_MODULES_DIR] }).css,
  }));
}

const COMPILED_STYLESHEETS = compiledStylesheets();

function indexHtml(): string {
  return readFileSync(join(APP_DIR, 'src', 'index.html'), 'utf8');
}

/** An @font-face names the face it declares, which is not a use of it. */
function withoutFontFaces(css: string): string {
  return css.replace(/@font-face\s*\{[^}]*\}/g, '');
}

function fontFamilyValues(source: string): string[] {
  return (source.match(/font-family\s*:[^;}]+/g) ?? []).map((match) =>
    match.slice(match.indexOf(':') + 1).trim()
  );
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
  for (const { stylesheet, css } of COMPILED_STYLESHEETS) {
    for (const value of fontFamilyValues(withoutFontFaces(css))) {
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

function referencedFamilies(): Set<string> {
  const sources = [
    ...COMPILED_STYLESHEETS.map((entry) => withoutFontFaces(entry.css)),
    indexHtml(),
  ];
  const families = new Set<string>();
  for (const source of sources) {
    for (const value of fontFamilyValues(source)) {
      const literals = value
        .replace(/var\(\s*--[\w-]+\s*,?/g, '')
        .replace(/\)/g, '');
      for (const family of splitFamilies(literals)) families.add(family);
    }
  }
  return families;
}

function fontFaces(): FontFace[] {
  return COMPILED_STYLESHEETS.flatMap(({ css }) =>
    (css.match(/@font-face\s*\{[^}]*\}/g) ?? []).flatMap((block) => {
      const family = /font-family\s*:\s*([^;}]+)/.exec(block)?.[1];
      return family
        ? [
            {
              family: splitFamilies(family)[0],
              urls: [...block.matchAll(/url\(\s*['"]?([^'")]+)['"]?\s*\)/g)].map((match) => match[1]),
            },
          ]
        : [];
    })
  );
}

function loadedWebFontFamilies(): Set<string> {
  return new Set(
    fontFaces()
      .filter((face) => face.urls.some((url) => url.startsWith(SELF_HOSTED_FONT_PATH)))
      .map((face) => face.family)
  );
}

/** Where the build copies each file served under /assets/fonts/ from. */
function fontAssetInputs(): string[] {
  return buildOptions()
    .assets.filter((asset): asset is AssetGlob => typeof asset !== 'string')
    .filter((asset) => `/${asset.output.replace(/^\/|\/$/g, '')}/` === SELF_HOSTED_FONT_PATH)
    .map((asset) => join(FRONTEND_DIR, asset.input));
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

describe(`web fonts are self-hosted (${APP_NAME})`, () => {
  it('loads nothing from Google Fonts, which would hand each visitor\'s IP address to Google', () => {
    const sources = [indexHtml(), ...COMPILED_STYLESHEETS.map((entry) => entry.css)];

    expect(sources.filter((source) => GOOGLE_FONT_HOSTS.test(source))).toEqual([]);
  });

  it('ships every font file a self-hosted face names', () => {
    const inputs = fontAssetInputs();
    const files = fontFaces()
      .flatMap((face) => face.urls)
      .filter((url) => url.startsWith(SELF_HOSTED_FONT_PATH))
      .map((url) => url.slice(SELF_HOSTED_FONT_PATH.length));

    expect(inputs.length).toBeGreaterThan(0);
    expect(files.length).toBeGreaterThan(0);
    expect(files.filter((file) => !inputs.some((input) => existsSync(join(input, file))))).toEqual([]);
  });
});

describe(`web font requests — every requested family is used (${APP_NAME})`, () => {
  it('names every requested family in a font-family declaration', () => {
    const loaded = loadedWebFontFamilies();
    const referenced = referencedFamilies();
    expect(loaded.size).toBeGreaterThan(0);
    expect(referenced.size).toBeGreaterThan(0);
    expect([...loaded].filter((family) => !referenced.has(family))).toEqual([]);
  });
});
