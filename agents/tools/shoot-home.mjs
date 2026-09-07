// Screenshot the live customer home page, section by section, so the rendered
// result can be compared against the approved design instead of assumed.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';

// Playwright is a dependency of the Nx workspace, not of this folder, and ESM
// resolves from the SCRIPT's directory rather than the cwd. Resolve it against
// the working directory so the harness can live outside src/Cleansia.App.
const require = createRequire(`${process.cwd()}/`);
const { chromium } = require('playwright');

const URL = process.env.TARGET ?? 'http://localhost:4202/';
const OUT = process.env.OUT ?? 'shots';
const WIDTH = Number(process.env.WIDTH ?? 1440);

mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({
  viewport: { width: WIDTH, height: 1000 },
  deviceScaleFactor: 1,
});

// Render in the locale under review. The design is Czech, and copy that reads
// correctly in English can still be wrong or overflow in cs/uk/ru.
const LANG = process.env.LANG_CODE;
if (LANG) {
  await page.addInitScript((lang) => {
    try { localStorage.setItem('preferred_language', lang); } catch { /* private mode */ }
  }, LANG);
}

const errors = [];
page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));
page.on('pageerror', (e) => errors.push(String(e)));

await page.goto(URL, { waitUntil: 'networkidle', timeout: 60000 });
await page.waitForTimeout(1200);

// Scroll the whole page first: sections carry `animate-on-scroll` and stay at
// opacity 0 until an IntersectionObserver reveals them, so a fullPage shot taken
// without scrolling photographs blank bands and hides exactly what we are here
// to check.
await page.evaluate(async () => {
  const step = Math.round(window.innerHeight * 0.8);
  for (let y = 0; y < document.body.scrollHeight; y += step) {
    window.scrollTo(0, y);
    await new Promise((r) => setTimeout(r, 120));
  }
  window.scrollTo(0, 0);
});
await page.waitForTimeout(900);

// The reveal is an IntersectionObserver adding `anim-pending` (opacity 0) and
// removing it on intersect. It does not always fire for every section under
// automation, and a screenshot of an unrevealed section is a photograph of the
// harness rather than of the page. Strip the gate so what is photographed is
// the real content; the animation itself is not what these shots verify.
await page.addStyleTag({
  content: '.animate-on-scroll,.anim-pending{opacity:1!important;transform:none!important;filter:none!important}',
});
await page.waitForTimeout(500);

// Dismiss the cookie banner if it is covering the hero.
for (const label of ['Accept All', 'Přijmout vše', 'Prijať všetko']) {
  const b = page.getByRole('button', { name: label });
  if (await b.count()) { await b.first().click().catch(() => {}); break; }
}
await page.waitForTimeout(400);

// Then the outline as the DOM actually reports it — headings in document order,
// which is the thing that has to match the design's nine sections.
const outline = await page.evaluate(() => {
  const seen = [];
  document.querySelectorAll('section, footer, nav, h1, h2, h3').forEach((el) => {
    const tag = el.tagName.toLowerCase();
    // textContent, not innerText: innerText returns '' for anything the browser
    // has not laid out or that an ancestor has hidden, which made off-screen
    // sections look empty when they were fine.
    const text = (el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 90);
    const cls = (el.className && typeof el.className === 'string' ? el.className : '').slice(0, 70);
    if (tag === 'section' || tag === 'footer' || tag === 'nav') {
      seen.push({ kind: tag, cls, first: text });
    } else {
      seen.push({ kind: tag, cls, text });
    }
  });
  return seen;
});
writeFileSync(`${OUT}/outline.json`, JSON.stringify(outline, null, 2));

// One shot per top-level section, named by its class, so a diff is readable.
const sections = await page.$$('section, footer');
for (let i = 0; i < sections.length; i++) {
  const cls = await sections[i].evaluate((el) => (typeof el.className === 'string' ? el.className : ''));
  const slug = (cls.split(/\s+/)[0] || `sec${i}`).replace(/[^\w-]/g, '');
  try {
    await sections[i].scrollIntoViewIfNeeded();
    await page.waitForTimeout(250);
    await sections[i].screenshot({ path: `${OUT}/${String(i + 1).padStart(2, '0')}-${slug}.png` });
  } catch {
    /* a zero-height section cannot be shot; the outline still records it */
  }
}

// The full-page shot goes LAST, once every section has been scrolled into view:
// the reveal is driven by an IntersectionObserver, so a capture taken earlier
// photographs blank bands and hides exactly what these shots exist to check.
await page.evaluate(() => window.scrollTo(0, 0));
await page.waitForTimeout(700);
await page.screenshot({ path: `${OUT}/00-full.png`, fullPage: true });

writeFileSync(`${OUT}/console-errors.txt`, errors.join('\n'));

console.log(`sections: ${sections.length}`);
console.log(`console errors: ${errors.length}`);
for (const o of outline) {
  if (o.kind === 'h1' || o.kind === 'h2') console.log(`  ${o.kind}  ${o.text}`);
}

await browser.close();
