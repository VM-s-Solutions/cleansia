/**
 * What colour is behind each section, and where does the page change ground?
 *
 * The approved artboard paints exactly four grounds: the hero gradient, the
 * gallery's #F0F9FF band, the CTA gradient and the navy footer. Everything else
 * is white. Any other band is a seam the visitor reads as a broken transition,
 * and seams are invisible in a screenshot taken at the wrong scroll offset —
 * hence a probe rather than an eye.
 */
import { createRequire } from 'node:module';
const { chromium } = createRequire(`${process.cwd()}/`)('playwright');

const TARGET = process.env.TARGET ?? 'http://localhost:4202/';
// Same convention as the other home checkers: THEME=light|dark, default light.
const THEME = process.env.THEME ?? 'light';
const DARK = THEME === 'dark';

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
await page.addInitScript((t) => { try { localStorage.setItem('cleansia-theme', t); } catch { /* private mode */ } }, THEME);
await page.goto(TARGET, { waitUntil: 'networkidle' });
await page.evaluate(async () => {
  for (let y = 0; y < document.body.scrollHeight; y += 600) {
    window.scrollTo(0, y);
    await new Promise((r) => setTimeout(r, 60));
  }
  window.scrollTo(0, 0);
});
await page.waitForTimeout(400);

const rows = await page.evaluate(() => {
  const out = [];
  const walk = (el) => {
    const cs = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    if (r.height < 24 || r.width < 400) return;
    const bg = cs.backgroundImage !== 'none' ? `gradient` : cs.backgroundColor;
    if (bg === 'rgba(0, 0, 0, 0)') return;
    out.push({
      tag: el.tagName.toLowerCase(),
      cls: (el.className || '').toString().split(' ').filter((c) => c.startsWith('cl-') || c.startsWith('cleansia')).join('.'),
      top: Math.round(r.top + scrollY),
      h: Math.round(r.height),
      bg,
    });
  };
  document.querySelectorAll('main, main *, footer, footer *').forEach(walk);
  return out;
});

// Only the outermost painter at each offset matters; a card on white is not a band.
const bands = rows.filter((r) => r.h > 120).sort((a, b) => a.top - b.top);
console.log(`grounds painted (${DARK ? 'dark' : 'light'}):`);
const seen = new Set();
for (const b of bands) {
  const key = `${b.top}:${b.bg}`;
  if (seen.has(key)) continue;
  seen.add(key);
  console.log(`  ${String(b.top).padStart(6)}px  h=${String(b.h).padStart(5)}  ${b.bg.padEnd(24)} ${b.tag}.${b.cls}`);
}

const foam = await page.evaluate(() =>
  [...document.querySelectorAll('cleansia-foam-edge svg')].map((s) => ({
    vb: s.getAttribute('viewBox'),
    h: Math.round(s.getBoundingClientRect().height),
    top: Math.round(s.getBoundingClientRect().top + scrollY),
    // The LAST path, not the first: the strip draws its rim behind the foam, so
    // `querySelector` returns the crescent's colour rather than the fill that
    // has to match the section the edge leads into — which is the one worth
    // reporting, because a mismatch there is the hard band this design avoids.
    fill: getComputedStyle([...s.querySelectorAll('path')].at(-1)).fill,
    rim: getComputedStyle(s.querySelector('path')).fill,
  }))
);
console.log(`\nfoam edges: ${foam.length} (design has 4)`);
foam.forEach((f) => console.log(`  ${String(f.top).padStart(6)}px  ${f.vb.padEnd(14)} rendered=${f.h}px  fill=${f.fill}  rim=${f.rim}`));

await browser.close();
