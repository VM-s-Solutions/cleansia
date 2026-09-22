import { existsSync, readdirSync, readFileSync } from 'fs';
import { dirname, join, relative } from 'path';
import { compile } from 'sass';

const APP_NAME = 'cleansia.app';

const ALL_APPS = ['cleansia.app', 'cleansia-partner.app', 'cleansia-admin.app'];

const TEMPLATE_ROOTS = [
  'apps/cleansia.app/src',
  'libs/cleansia-customer-features',
  'libs/shared/components',
];

/**
 * Third-party stylesheets an index.html may still load over the network.
 * Shrink-only: an entry leaves when its sheet is self-hosted, and a new one
 * is argued for here rather than added to the HTML.
 */
const REMAINING_THIRD_PARTY_STYLESHEETS = ['fonts.googleapis.com/css2'];

interface CssRule {
  media: string;
  selector: string;
  declarations: string;
}

interface ClassUse {
  className: string;
  site: string;
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
// The build resolves bare package paths in the stylesheets; the plain compiler needs telling where.
const NODE_MODULES_DIR = join(FRONTEND_DIR, 'node_modules');

function indexHtml(app: string): string {
  return readFileSync(join(FRONTEND_DIR, 'apps', app, 'src', 'index.html'), 'utf8');
}

function thirdPartyStylesheetHrefs(html: string): string[] {
  const hrefs: string[] = [];
  for (const link of html.match(/<link\b[^>]*>/g) ?? []) {
    if (!/rel="stylesheet"/.test(link)) continue;
    const href = link.match(/href="([^"]+)"/)?.[1];
    if (href && /^https?:\/\//.test(href)) hrefs.push(href);
  }
  return hrefs;
}

function normalizeDeclarations(block: string): string {
  return block
    .split(';')
    .map((declaration) => declaration.replace(/\s+/g, ' ').trim())
    .filter((declaration) => declaration.length > 0)
    .join('; ');
}

function parseRules(css: string): CssRule[] {
  const rules: CssRule[] = [];
  const source = css
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/@(?:charset|import|use|forward)\b[^;{]*;/g, '');
  const walk = (from: number, to: number, media: string): void => {
    let cursor = from;
    while (cursor < to) {
      const open = source.indexOf('{', cursor);
      if (open === -1 || open >= to) break;
      const prelude = source.slice(cursor, open).replace(/\s+/g, ' ').trim();
      let depth = 1;
      let close = open + 1;
      while (close < to && depth > 0) {
        if (source[close] === '{') depth++;
        else if (source[close] === '}') depth--;
        close++;
      }
      const bodyFrom = open + 1;
      const bodyTo = close - 1;
      if (prelude.startsWith('@media')) {
        walk(bodyFrom, bodyTo, prelude);
      } else if (!prelude.startsWith('@')) {
        const declarations = normalizeDeclarations(source.slice(bodyFrom, bodyTo));
        for (const selector of prelude.split(',')) {
          rules.push({ media, selector: selector.trim(), declarations });
        }
      }
      cursor = close;
    }
  };
  walk(0, source.length, '');
  return rules;
}

function ruleKey(media: string, selector: string): string {
  return `${media}|${selector}`;
}

function groupByKey(rules: CssRule[]): Map<string, CssRule[]> {
  const groups = new Map<string, CssRule[]>();
  for (const rule of rules) {
    const key = ruleKey(rule.media, rule.selector);
    groups.set(key, [...(groups.get(key) ?? []), rule]);
  }
  return groups;
}

function primeFlexRules(): CssRule[] {
  return parseRules(
    readFileSync(join(FRONTEND_DIR, 'node_modules', 'primeflex', 'primeflex.css'), 'utf8')
  );
}

function selectorFor(className: string): string {
  return `.${className.replace(/:/g, '\\:')}`;
}

function primeFlexClassNames(rules: CssRule[]): Set<string> {
  const names = new Set<string>();
  for (const { selector } of rules) {
    const match = selector.match(/^\.((?:[\w-]|\\:)+)$/);
    if (match) names.add(match[1].replace(/\\:/g, ':'));
  }
  return names;
}

function walkFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== 'node_modules') walkFiles(path, out);
    } else if (/\.(html|ts)$/.test(entry.name) && !/\.spec\.ts$/.test(entry.name)) {
      out.push(path);
    }
  }
  return out;
}

function classTokens(line: string): string[] {
  const attributes = [
    ...line.matchAll(/\b(?:class|styleClass|panelStyleClass|inputStyleClass)="([^"]*)"/g),
  ].map((match) => match[1]);
  const quotedInBindings = [
    ...line.matchAll(/\[(?:class|ngClass|styleClass)\]="([^"]*)"/g),
  ].flatMap((match) => [...match[1].matchAll(/'([^']*)'/g)].map((inner) => inner[1]));
  const propertyBindings = [...line.matchAll(/\[class\.([^\]]+)\]/g)].map((match) => match[1]);
  return [...attributes, ...quotedInBindings]
    .flatMap((value) => value.split(/\s+/))
    .concat(propertyBindings)
    .filter((token) => token.length > 0);
}

function primeFlexClassUses(universe: Set<string>): ClassUse[] {
  const uses: ClassUse[] = [];
  for (const root of TEMPLATE_ROOTS) {
    for (const file of walkFiles(join(FRONTEND_DIR, root))) {
      const site = relative(FRONTEND_DIR, file).split('\\').join('/');
      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          for (const token of classTokens(line)) {
            if (universe.has(token)) uses.push({ className: token, site: `${site}:${index + 1}` });
          }
        });
    }
  }
  return uses;
}

function compiledBuildCss(): string {
  const project = JSON.parse(
    readFileSync(join(APP_DIR, 'project.json'), 'utf8')
  ) as ProjectConfiguration;
  return project.targets.build.options.styles
    .map((stylesheet) => compile(join(FRONTEND_DIR, stylesheet), { quietDeps: true, loadPaths: [NODE_MODULES_DIR] }).css)
    .join('\n');
}

function unique(values: string[]): string[] {
  return [...new Set(values)];
}

describe(`PrimeFlex is self-hosted as the subset the templates use (${APP_NAME})`, () => {
  const primeFlex = groupByKey(primeFlexRules());
  const universe = primeFlexClassNames([...primeFlex.values()].flat());
  const uses = primeFlexClassUses(universe);
  const usedClassNames = unique(uses.map((use) => use.className)).sort();
  const bundled = groupByKey(parseRules(compiledBuildCss()));

  const packageKeysFor = (className: string): string[] =>
    [...primeFlex.keys()].filter((key) => key.endsWith(`|${selectorFor(className)}`));

  it('reads the package, the templates and the build stylesheets', () => {
    expect(universe.size).toBeGreaterThan(1000);
    expect(usedClassNames.length).toBeGreaterThan(20);
    expect(bundled.size).toBeGreaterThan(0);
  });

  it('sees the responsive variants the navbar breakpoint depends on', () => {
    expect(usedClassNames).toEqual(expect.arrayContaining(['md:flex', 'md:hidden']));
    expect(packageKeysFor('md:flex')).toEqual(['@media screen and (min-width: 768px)|.md\\:flex']);
  });

  it('bundles a rule for every PrimeFlex class a customer-reachable template uses', () => {
    const missing = uses.filter((use) =>
      packageKeysFor(use.className).some((key) => !bundled.has(key))
    );
    expect(unique(missing.map((use) => `${use.className} @ ${use.site}`))).toEqual([]);
  });

  it('keeps every bundled rule for a used class declaration-identical to the package', () => {
    const drifted: string[] = [];
    for (const className of usedClassNames) {
      for (const key of packageKeysFor(className)) {
        const expected = unique((primeFlex.get(key) ?? []).map((rule) => rule.declarations));
        for (const rule of bundled.get(key) ?? []) {
          if (!expected.includes(rule.declarations)) {
            drifted.push(`${key} → "${rule.declarations}" (package: "${expected.join('" | "')}")`);
          }
        }
      }
    }
    expect(drifted).toEqual([]);
  });

  it('keeps the compound field rules the package ships beside .field', () => {
    for (const selector of ['.field > label', '.field > small']) {
      const key = ruleKey('', selector);
      expect(bundled.get(key)?.map((rule) => rule.declarations)).toEqual(
        primeFlex.get(key)?.map((rule) => rule.declarations)
      );
    }
  });
});

describe('no index.html loads a stylesheet from a CDN this repo has not argued for', () => {
  const hrefsByApp = new Map(
    ALL_APPS.map((app) => [app, thirdPartyStylesheetHrefs(indexHtml(app))])
  );

  it('never requests PrimeFlex from a CDN in any app', () => {
    for (const app of ALL_APPS) {
      expect({ app, mentionsPrimeFlex: /primeflex/i.test(indexHtml(app)) }).toEqual({
        app,
        mentionsPrimeFlex: false,
      });
    }
  });

  it('loads only the remaining allow-listed third-party stylesheets', () => {
    for (const [app, hrefs] of hrefsByApp) {
      const unlisted = hrefs.filter(
        (href) => !REMAINING_THIRD_PARTY_STYLESHEETS.some((allowed) => href.includes(allowed))
      );
      expect({ app, unlisted }).toEqual({ app, unlisted: [] });
    }
  });

  it('shrinks the allow-list once a sheet is self-hosted', () => {
    const stillLoaded = REMAINING_THIRD_PARTY_STYLESHEETS.filter((allowed) =>
      [...hrefsByApp.values()].some((hrefs) => hrefs.some((href) => href.includes(allowed)))
    );
    expect(stillLoaded).toEqual(REMAINING_THIRD_PARTY_STYLESHEETS);
  });
});
