/**
 * Turn an approved artboard into a machine-checkable spec, and check a live
 * page against it.
 *
 * Why this exists: the home page was verified with 51 assertions I typed out of
 * the artboard by hand. That worked, but it only ever catches what I remembered
 * to write down, and it does not scale to the twenty-odd screens left. The
 * artboards carry every value as an inline style, so the numbers can be READ
 * rather than transcribed.
 *
 * Two kinds of assertion, and the split is deliberate:
 *
 *   TEXT-ANCHORED (automatic, hundreds of them). Every run of visible text in
 *   the artboard is matched to the run with the same words in the running page,
 *   and their typography is compared. No mapping to write, and nothing can be
 *   forgotten - if the copy is on both, the assertion exists.
 *
 *   BOX (declared, a dozen per screen). Padding, gap, radius and the like have
 *   no text to anchor them, and the artboard's markup is not the app's. So a
 *   small map pairs one app selector with one artboard selector - but the
 *   EXPECTED VALUES still come out of the artboard, so the map says only which
 *   box is which, never what it should measure.
 *
 * Usage:
 *   node agents/tools/artboard-spec.mjs extract <artboard.dc.html> <spec.json> [--map map.json]
 *   node agents/tools/artboard-spec.mjs check   <spec.json> <url> [--lang cs] [--width 1440]
 *
 * Run it from src/Cleansia.App - that is where playwright is installed.
 */
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { readFileSync, writeFileSync } from 'node:fs';

const { chromium } = createRequire(`${process.cwd()}/`)('playwright');

// An artboard is a Design Component: <x-dc> wraps the design and <helmet> holds
// the stylesheet. Neither is a known element, so both default to inline.
const DC_FIX = 'x-dc{display:block!important} helmet{display:none!important}';

/** Properties compared on a run of text. */
const TEXT_PROPS = ['fontSize', 'fontWeight', 'lineHeight', 'letterSpacing', 'color', 'textTransform', 'textAlign'];
/** Properties compared on a box. */
const BOX_PROPS = [
  'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
  'marginTop', 'marginBottom', 'rowGap', 'columnGap',
  'borderRadius', 'minHeight', 'minWidth', 'backgroundColor', 'borderTopWidth', 'borderTopColor',
  'display', 'gridTemplateColumns', 'flexDirection', 'alignItems', 'justifyContent',
];
/**
 * Typography, compared only on a box whose map entry opts in with
 * `"typography": true`. It is opt-in because these five inherit: on a layout
 * container that holds no words of its own they report the document's base size
 * (14px in the app, 16px in a bare artboard) and say nothing about the design.
 * On a text-bearing leaf whose words are sample data - a package's include list
 * - this is the ONLY thing that asserts its size and colour, because ignoring
 * the sample text ignores the styling with it.
 */
const TYPE_PROPS = ['fontSize', 'fontWeight', 'lineHeight', 'color', 'whiteSpace'];

/**
 * The text of an element, ignoring anything a child already owns - so a card
 * does not claim the words of the heading inside it, and every run is asserted
 * exactly once.
 */
const OWN_TEXT_FN = `(el) => {
  let out = '';
  for (const n of el.childNodes) if (n.nodeType === 3) out += n.textContent;
  return out.replace(/\\u00a0/g, ' ').replace(/\\s+/g, ' ').trim();
}`;

async function walk(page, props) {
  return page.evaluate(
    ({ textProps, boxProps, ownTextSrc }) => {
      const ownText = eval(ownTextSrc);
      const visible = (el) => {
        const cs = getComputedStyle(el);
        if (cs.display === 'none' || cs.visibility === 'hidden') return false;
        const r = el.getBoundingClientRect();
        return r.width > 0 && r.height > 0;
      };
      const norm = (k, v) => (k === 'textAlign' && (v === 'start' || v === 'left') ? 'left' : v);
      const pick = (cs, keys) => Object.fromEntries(keys.map((k) => [k, norm(k, cs[k])]));
      const texts = [];
      for (const el of document.querySelectorAll('body *')) {
        if (!visible(el)) continue;
        const t = ownText(el);
        // Two characters is a bullet or a separator, not a claim worth pinning.
        if (t.length < 3) continue;
        texts.push({ text: t, tag: el.tagName.toLowerCase(), style: pick(getComputedStyle(el), textProps) });
      }
      return { texts, boxProps };
    },
    { textProps: props.text, boxProps: props.box, ownTextSrc: OWN_TEXT_FN }
  );
}

async function boxes(page, selectors, typographyFor = {}) {
  return page.evaluate(
    ({ sels, keys, typeKeys, withType }) => {
      const out = {};
      for (const [name, sel] of Object.entries(sels)) {
        const el = document.querySelector(sel);
        if (!el) { out[name] = { missing: sel }; continue; }
        const cs = getComputedStyle(el);
        const wanted = withType[name] ? [...keys, ...typeKeys] : keys;
        out[name] = Object.fromEntries(wanted.map((k) => [k, cs[k]]));
      }
      return out;
    },
    { sels: selectors, keys: BOX_PROPS, typeKeys: TYPE_PROPS, withType: typographyFor }
  );
}

async function openArtboard(browser, file, width) {
  const page = await browser.newPage({ viewport: { width, height: 1400 }, deviceScaleFactor: 1 });
  await page.goto(pathToFileURL(file).href, { waitUntil: 'domcontentloaded' });
  await page.addStyleTag({ content: DC_FIX });
  await page.evaluate(() => document.fonts?.ready);
  await page.waitForTimeout(700);
  return page;
}

async function openApp(browser, url, { width, lang, theme, advance = 0 }) {
  const page = await browser.newPage({ viewport: { width, height: 1400 }, deviceScaleFactor: 1 });
  await page.addInitScript((cfg) => {
    try {
      localStorage.setItem('preferred_language', cfg.lang);
      localStorage.setItem('cleansia-theme', cfg.theme);
      localStorage.setItem('cleansia-customer-cookie-consent', 'accepted');
    } catch { /* private mode */ }
  }, { lang, theme });
  await page.goto(url, { waitUntil: 'networkidle', timeout: 90000 });
  // Checking an artboard against its own spec is the self-test for this tool:
  // if extract and check disagree the machinery is wrong, not the page.
  if (url.endsWith('.dc.html')) await page.addStyleTag({ content: DC_FIX });
  await page.evaluate(async () => {
    for (let y = 0; y < document.body.scrollHeight; y += 400) { window.scrollTo(0, y); await new Promise((r) => setTimeout(r, 40)); }
    window.scrollTo(0, 0);
  });
  // A selected state is part of the design: the artboard draws one chip reading
  // "Přidáno", and a page measured with nothing chosen can never show it. One
  // click covers both labels.
  const first = page.locator('[data-spec-select]').first();
  if (await first.count()) await first.click().catch(() => {});

  // A later step of a wizard is only reachable through the earlier ones, so the
  // checker walks forward the same way a visitor does rather than deep-linking
  // into a state the app would never be in.
  for (let i = 0; i < advance; i += 1) {
    await page.waitForTimeout(500);
    const next = page.locator('[data-spec-advance]').first();
    if (!(await next.count())) break;
    await next.click().catch(() => {});
  }

  await page.evaluate(() => document.fonts?.ready);
  await page.waitForTimeout(900);
  return page;
}

// ─────────────────────────────────────────────────────────────── extract
async function extract(artboard, out, mapFile) {
  const raw = mapFile ? JSON.parse(readFileSync(mapFile, 'utf8')) : {};
  // `_ignore` lists text the artboard shows as a SAMPLE - a price, a name, an
  // address - which the running app will render from real data. Without it every
  // sample value reports as missing copy and buries the real gaps.
  const ignore = (raw._ignore ?? []).map((p) => new RegExp(p));
  // Every underscore key is metadata for this file, not a selector to query.
  // A value is either the artboard selector, or {sel, skip, why} where `skip`
  // drops properties the artboard is knowingly stale on - each with its reason.
  const map = Object.fromEntries(Object.entries(raw)
    .filter(([k]) => !k.startsWith('_'))
    .map(([app, v]) => [app, typeof v === 'string'
      ? { sel: v, skip: [], typography: false }
      : { sel: v.sel, skip: v.skip ?? [], typography: v.typography === true }]));
  const browser = await chromium.launch({ args: ['--hide-scrollbars'] });
  const page = await openArtboard(browser, artboard, 1440);
  const { texts } = await walk(page, { text: TEXT_PROPS, box: BOX_PROPS });
  // The map is { appSelector: artboardSelector }; values are read off the artboard.
  const artSelectors = Object.fromEntries(Object.entries(map).map(([app, m]) => [app, m.sel]));
  const boxValues = await boxes(page, artSelectors,
    Object.fromEntries(Object.entries(map).map(([app, m]) => [app, m.typography])));
  for (const [app, m] of Object.entries(map)) {
    for (const k of m.skip) delete boxValues[app]?.[k];
  }
  await browser.close();

  // A repeated string (every "Přidat" chip) cannot be matched to one element, so
  // it is asserted once and only where every copy agrees.
  const byText = new Map();
  for (const t of texts) {
    const prev = byText.get(t.text);
    if (!prev) { byText.set(t.text, { ...t, count: 1 }); continue; }
    prev.count += 1;
    for (const k of TEXT_PROPS) if (prev.style[k] !== t.style[k]) prev.style[k] = null;
  }
  const spec = {
    artboard,
    generatedFrom: '1440px, light',
    ignore: (raw._ignore ?? []),
    texts: [...byText.values()].filter((t) => !ignore.some((r) => r.test(t.text))).map((t) => ({
      text: t.text,
      count: t.count,
      style: Object.fromEntries(Object.entries(t.style).filter(([, v]) => v !== null)),
    })),
    boxes: boxValues,
  };
  writeFileSync(out, JSON.stringify(spec, null, 2) + '\n', 'utf8');
  const assertions = spec.texts.reduce((n, t) => n + Object.keys(t.style).length, 0)
    + Object.values(spec.boxes).reduce((n, b) => n + Object.keys(b).length, 0);
  console.log(`extracted ${spec.texts.length} text runs and ${Object.keys(spec.boxes).length} boxes`
    + ` — ${assertions} assertions — from ${artboard}`);
}

// ───────────────────────────────────────────────────────────────── check
async function check(specFile, url, opts) {
  const spec = JSON.parse(readFileSync(specFile, 'utf8'));
  const browser = await chromium.launch({ args: ['--hide-scrollbars'] });
  const page = await openApp(browser, url, opts);
  const { texts } = await walk(page, { text: TEXT_PROPS, box: BOX_PROPS });
  const appSelectors = Object.fromEntries(Object.keys(spec.boxes).map((s) => [s, s]));
  const boxValues = await boxes(page, appSelectors,
    Object.fromEntries(Object.entries(spec.boxes).map(([s, b]) => [s, 'fontSize' in b])));
  await browser.close();

  const live = new Map();
  for (const t of texts) {
    const prev = live.get(t.text);
    if (!prev) { live.set(t.text, { ...t, count: 1 }); continue; }
    prev.count += 1;
    for (const k of TEXT_PROPS) if (prev.style[k] !== t.style[k]) prev.style[k] = null;
  }

  const diffs = [];
  const missing = [];
  const ignore = (spec.ignore ?? []).map((p) => new RegExp(p));
  for (const want of spec.texts) {
    const got = live.get(want.text);
    if (!got) { if (!ignore.some((r) => r.test(want.text))) missing.push(want.text); continue; }
    for (const [k, v] of Object.entries(want.style)) {
      if (got.style[k] == null) continue; // the live page disagrees with itself; reported below
      if (got.style[k] !== v) diffs.push({ where: `text "${want.text.slice(0, 46)}"`, prop: k, want: v, got: got.style[k] });
    }
  }
  for (const [sel, want] of Object.entries(spec.boxes)) {
    const got = boxValues[sel];
    if (want.missing) { missing.push(`artboard box ${want.missing}`); continue; }
    if (!got || got.missing) { missing.push(`app box ${sel}`); continue; }
    for (const [k, v] of Object.entries(want)) {
      if (got[k] !== v) diffs.push({ where: `box ${sel}`, prop: k, want: v, got: got[k] });
    }
  }

  console.log(`checked ${spec.texts.length} text runs and ${Object.keys(spec.boxes).length} boxes`
    + ` at ${opts.width}px, lang=${opts.lang}, ${opts.theme}`);
  for (const m of missing) console.log(`  MISSING  ${m}`);
  for (const d of diffs) console.log(`  DIFF     ${d.where.padEnd(52)} ${d.prop}: ${d.got} != ${d.want}`);
  const total = diffs.length + missing.length;
  console.log(total ? `\n${diffs.length} mismatch(es), ${missing.length} missing` : '\nthe page matches the artboard');
  process.exit(total ? 1 : 0);
}

// ────────────────────────────────────────────────────────────────── main
const [, , cmd, a, b] = process.argv;
const flag = (name, dflt) => {
  const i = process.argv.indexOf(`--${name}`);
  return i > -1 ? process.argv[i + 1] : dflt;
};
if (cmd === 'extract') await extract(a, b, flag('map', null));
else if (cmd === 'check') await check(a, b, {
  width: Number(flag('width', 1440)),
  lang: flag('lang', 'cs'),
  theme: flag('theme', 'light'),
  advance: Number(flag('advance', 0)),
});
else {
  console.log('usage: artboard-spec.mjs extract <artboard.dc.html> <spec.json> [--map map.json]');
  console.log('       artboard-spec.mjs check <spec.json> <url> [--lang cs] [--width 1440] [--theme light] [--advance N]');
  process.exit(2);
}
