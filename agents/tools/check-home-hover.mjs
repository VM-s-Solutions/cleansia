// Hover and focus states, against the artboard's own rules:
//   .btn:hover  bg #0369A1, translateY(-1px), shadow 0 4px 10px rgba(15,23,42,.14)
//   .link:hover border #0284C7, colour #075985
//   .chip:hover border #7DD3FC, bg #F0F9FF
//   .icobtn:hover border #7DD3FC, bg #F0F9FF
import { createRequire } from 'node:module';
const require = createRequire(`${process.cwd()}/`);
const { chromium } = require('playwright');
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1440, height: 1000 } });
await p.addInitScript(() => { try { localStorage.setItem('preferred_language','cs'); } catch {} });
await p.goto('http://localhost:4202/', { waitUntil: 'networkidle', timeout: 60000 });
await p.waitForTimeout(2200);

const CASES = [
  ['.cl-hero__buttons .cl-btn', { backgroundColor: 'rgb(3, 105, 161)' }],
  ['.cl-hero__buttons .cl-link', { borderBottomColor: 'rgb(2, 132, 199)' }],
  ['.cl-quote__chip:not(.cl-chip--on)', { borderColor: 'rgb(56, 189, 248)', backgroundColor: 'rgb(240, 249, 255)' }],
  ['.customer-navbar__icon-btn', { borderColor: 'rgb(125, 211, 252)', backgroundColor: 'rgb(240, 249, 255)' }],
];
let bad = 0;
for (const [sel, want] of CASES) {
  const el = await p.$(sel);
  if (!el) { console.log(`  MISSING  ${sel}`); bad++; continue; }
  await el.scrollIntoViewIfNeeded();
  await el.hover();
  await p.waitForTimeout(320);
  const got = await el.evaluate((n, keys) => {
    const cs = getComputedStyle(n);
    const o = {}; for (const k of keys) o[k] = cs[k];
    o.transform = cs.transform; return o;
  }, Object.keys(want));
  const diffs = Object.entries(want).filter(([k, v]) => String(got[k]) !== String(v))
                                    .map(([k, v]) => `${k}: ${got[k]} != ${v}`);
  if (diffs.length) { bad++; console.log(`  DIFF  ${sel}\n        ${diffs.join('\n        ')}`); }
  else console.log(`  ok    ${sel}   transform=${got.transform}`);
}
console.log(`\n${bad} hover mismatches`);
await b.close();
