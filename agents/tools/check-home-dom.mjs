// Assert specific facts about the rendered DOM, so a stale dev-server compile
// is caught before a screenshot is trusted.
import { createRequire } from 'node:module';
const require = createRequire(`${process.cwd()}/`);
const { chromium } = require('playwright');
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1440, height: 900 } });
await p.addInitScript(() => { try { localStorage.setItem('preferred_language','cs'); } catch {} });
await p.goto('http://localhost:4202/', { waitUntil: 'networkidle', timeout: 60000 });
await p.waitForTimeout(2500);
const facts = await p.evaluate(() => ({
  quoteFootnote: !!document.querySelector('.cl-quote__footnote'),
  sizeChipSelected: !!document.querySelector('.cl-quote__chip.cl-chip--on'),
  langGlobe: !!document.querySelector('.cleansia-language-switcher--globe .pi-globe'),
  navNoWhiteBar: !document.querySelector('.customer-navbar.surface-overlay'),
  footerDeadRows: document.querySelectorAll('.cl-footer__soon').length,
  cookieBtn: !!document.querySelector('.cl-footer__linkbtn'),
  btnHeight: (() => { const el = document.querySelector('.cl-btn'); return el ? Math.round(el.getBoundingClientRect().height) : null; })(),
  mascotIcon: (() => { const el = document.querySelector('.cl-mascot-icon'); return el ? Math.round(el.getBoundingClientRect().height) : null; })(),
  rulesGap: (() => { const el = document.querySelector('.cl-rules'); return el ? getComputedStyle(el).gap : null; })(),
  contactAlign: (() => { const el = document.querySelector('.cl-footer__contact'); return el ? getComputedStyle(el).alignItems : null; })(),
}));
let bad = 0;
const want = { quoteFootnote: true, sizeChipSelected: true, langGlobe: true, navNoWhiteBar: true,
               footerDeadRows: 0, cookieBtn: true, btnHeight: 40, mascotIcon: 132,
               rulesGap: '24px', contactAlign: 'center' };
for (const [k, v] of Object.entries(want)) {
  const ok = String(facts[k]) === String(v);
  if (!ok) bad++;
  console.log(`  ${ok ? 'ok  ' : 'FAIL'} ${k.padEnd(18)} got=${facts[k]}  want=${v}`);
}
console.log(bad === 0 ? '\nall DOM assertions pass' : `\n${bad} FAILING`);
await b.close();
