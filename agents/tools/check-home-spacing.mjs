// Every padding, margin and gap the artboard declares, asserted against the
// live page. Values are read straight off the artboard's inline styles.
import { createRequire } from 'node:module';
const require = createRequire(`${process.cwd()}/`);
const { chromium } = require('playwright');

const CHECKS = [
  ['nav pill',            '.customer-navbar__inner', { padding: '10px 12px 10px 30px' }],
  ['nav links',           '.customer-navbar__center', { gap: '22px' }],
  ['hero body',           '.cl-hero__inner', { padding: '52px 64px 0px', gap: '48px', alignItems: 'end' }],
  // The artboard sets 16px, measured from the bottom of the eyebrow above it.
  // The eyebrow ("Praha a okolí do 30 km · denně 8-20") was removed on owner
  // ruling, so 30px is what now puts the h1 at the same distance from the top
  // of the band that the artboard draws.
  ['hero title',          '.cl-hero__title', { marginTop: '30px' }],
  ['hero sub',            '.cl-hero__subtitle', { margin: '18px 0px 0px' }],
  ['hero actions',        '.cl-hero__buttons', { gap: '26px', marginTop: '28px' }],
  ['quote card',          '.cl-quote', { padding: '28px 30px' }],
  ['promises section',    '.cl-section--promises', { padding: '20px 64px 0px' }],
  ['promises grid',       '.cl-promises', { gap: '20px', marginTop: '30px' }],
  ['promise card',        '.cl-promises__card', { padding: '30px 32px' }],
  ['promise heading',     '.cl-promises__heading', { marginTop: '10px' }],
  ['promise desc',        '.cl-promises__desc', { margin: '10px 0px 0px' }],
  ['services section',    '.cl-section--services', { padding: '64px 64px 0px' }],
  ['services head',       '.cl-services__head', { gap: '30px' }],
  ['services lead',       '.cl-services__lead', { margin: '10px 0px 0px' }],
  ['services grid',       '.cl-services', { gap: '20px', marginTop: '26px' }],
  ['service card',        '.cl-services__card', { padding: '30px 32px' }],
  ['service name',        '.cl-services__name', { marginTop: '18px' }],
  ['service desc',        '.cl-services__desc', { margin: '8px 0px 0px' }],
  ['service price',       '.cl-services__price', { marginTop: '14px' }],
  // The band carries no vertical padding of its own: the hood opens it, the cap
  // closes it, and the inner column holds the spacing. -> the approved artboard.
  ['gallery section',     '.cl-section--gallery', { paddingBottom: '0px', marginTop: '64px' }],
  ['gallery inner',       '.cl-section--gallery > .cl-section__inner', { paddingTop: '20px', paddingBottom: '36px' }],
  ['gallery grid',        '.cl-gallery', { gap: '22px', marginTop: '28px' }],
  ['gallery card',        '.cl-gallery__item', { padding: '14px' }],
  ['rules section',       '.cl-section--rules', { padding: '24px 64px 0px' }],
  ['rules card',          '.cl-rules__card', { padding: '30px 32px' }],
  ['rules value',         '.cl-rules__value', { marginTop: '8px' }],
  ['rules desc',          '.cl-rules__desc', { margin: '10px 0px 0px' }],
  ['plus section',        '.cl-section--plus', { padding: '64px 64px 0px' }],
  ['plus card',           '.cl-plus', { padding: '44px 50px' }],
  ['plus title',          '.cl-plus__title', { margin: '16px 0px 0px' }],
  ['plus perks',          '.cl-plus__perks', { gap: '9px 26px', marginTop: '18px' }],
  ['plus actions',        '.cl-plus__actions', { gap: '22px', marginTop: '24px' }],
  ['faq section',         '.cl-section--faq', { padding: '64px 64px 0px' }],
  ['faq grid',            '.cl-faq-layout', { gap: '56px' }],
  ['faq title',           '.cl-faq-layout__title', { marginTop: '16px' }],
  ['faq desc',            '.cl-faq-layout__desc', { margin: '12px 0px 0px' }],
  ['faq cta',             '.cl-faq-layout__cta', { marginTop: '22px', gap: '10px' }],
  ['faq header',          '.p-accordionheader', { padding: '22px 26px' }],
  ['cta band',            '.cl-cta', { marginTop: '64px' }],
  ['cta inner',           '.cl-cta__inner', { padding: '34px 64px 60px' }],
  ['cta card',            '.cl-cta__card', { padding: '40px 44px 36px' }],
  ['cta row1',            '.cl-cta__row--claim', { gap: '32px' }],
  ['cta desc',            '.cl-cta__desc', { margin: '8px 0px 0px' }],
  ['cta actions',         '.cl-cta__actions', { gap: '24px' }],
  ['cta row2',            '.cl-cta__row--promo', { marginTop: '32px', paddingTop: '28px', gap: '28px' }],
  ['footer body',         '.cl-footer__body', { padding: '56px 64px 0px' }],
  ['footer grid',         '.cl-footer__columns', { gap: '36px' }],
  ['footer desc',         '.cl-footer__brand-desc', { margin: '12px 0px 0px' }],
  ['footer apps',         '.cl-footer__apps', { gap: '10px', marginTop: '18px' }],
  ['footer app badge',    '.cl-footer__app', { gap: '8px' }],
];

const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1440, height: 1000 } });
await p.addInitScript(() => { try { localStorage.setItem('preferred_language','cs'); } catch {} });
await p.goto('http://localhost:4202/', { waitUntil: 'networkidle', timeout: 60000 });
await p.waitForTimeout(2500);
await p.evaluate(async () => { for (let y=0;y<document.body.scrollHeight;y+=600){window.scrollTo(0,y);await new Promise(r=>setTimeout(r,50));} window.scrollTo(0,0); });
await p.waitForTimeout(600);

const out = await p.evaluate((checks) => checks.map(([name, sel, want]) => {
  const el = document.querySelector(sel);
  if (!el) return { name, sel, missing: true };
  const cs = getComputedStyle(el);
  const diffs = [];
  for (const [k, v] of Object.entries(want)) {
    const got = cs[k];
    if (String(got) !== String(v)) diffs.push(`${k}: ${got} != ${v}`);
  }
  return { name, sel, diffs };
}), CHECKS);

let bad = 0;
for (const r of out) {
  if (r.missing) { bad++; console.log(`  MISSING  ${r.name.padEnd(20)} ${r.sel}`); continue; }
  if (r.diffs.length) { bad++; console.log(`  DIFF     ${r.name.padEnd(20)} ${r.diffs.join('  |  ')}`); }
}
console.log(`\n${bad} of ${CHECKS.length} spacing checks fail`);
await b.close();
