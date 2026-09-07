/**
 * Does anything above the fold move after first paint?
 *
 * The calculator grew by two chip rows when the service catalogue resolved,
 * which a screenshot cannot show and a human only notices as "it drags". This
 * samples the geometry of the hero's elements from first paint through the
 * network settling and reports anything whose top or height changed.
 */
import { createRequire } from 'node:module';
const { chromium } = createRequire(`${process.cwd()}/`)('playwright');

const TARGET = process.env.TARGET ?? 'http://localhost:4202/';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
// Pin the locale and dismiss consent, as the other home checkers do. Without
// this the first paint is in the browser default language and the second in the
// resolved one, so every measurement was really measuring the i18n bootstrap
// reflowing Czech text - not whether the page itself holds still.
await page.addInitScript((l) => {
  try {
    localStorage.setItem('preferred_language', l);
    localStorage.setItem('cleansia-customer-cookie-consent', 'accepted');
  } catch { /* private mode */ }
}, process.env.LANG_CODE ?? 'cs');

const SAMPLE = `(() => {
  const out = {};
  for (const sel of ['.cl-quote', '.cl-quote__result', '.cl-hero__image', '.cl-hero__buttons', '.cl-quote__when']) {
    const el = document.querySelector(sel);
    out[sel] = el ? { top: Math.round(el.getBoundingClientRect().top), h: Math.round(el.getBoundingClientRect().height) } : null;
  }
  return out;
})()`;

await page.goto(TARGET, { waitUntil: 'domcontentloaded' });
await page.waitForSelector('.cl-quote');
const first = await page.evaluate(SAMPLE);
await page.waitForLoadState('networkidle');
await page.waitForTimeout(1200);
const settled = await page.evaluate(SAMPLE);

let moved = 0;
for (const sel of Object.keys(first)) {
  const a = first[sel];
  const b = settled[sel];
  if (!a || !b) { console.log(`  n/a      ${sel}`); continue; }
  const dTop = b.top - a.top;
  const dH = b.h - a.h;
  // Sub-pixel rounding is not a shift; anything a reader would see is.
  const bad = Math.abs(dTop) > 2 || Math.abs(dH) > 2;
  if (bad) moved++;
  console.log(`  ${bad ? 'SHIFT' : 'ok   '}  ${sel.padEnd(22)} top ${a.top}->${b.top} (${dTop >= 0 ? '+' : ''}${dTop})  h ${a.h}->${b.h} (${dH >= 0 ? '+' : ''}${dH})`);
}
console.log(moved ? `\n${moved} element(s) move after first paint` : '\nnothing above the fold moves after first paint');
await browser.close();
process.exit(moved ? 1 : 0);
