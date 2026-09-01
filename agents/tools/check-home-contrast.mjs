import { createRequire } from 'node:module';
const require = createRequire(`${process.cwd()}/`);
const { chromium } = require('playwright');
import { walkWizard } from './wizard-walk.mjs';

const THEME = process.env.THEME ?? 'dark';
// Honour TARGET like the other checkers: this was pinned to the home page, so a
// run against any other route silently measured the home page and reported a
// green that was about a different page entirely.
const TARGET = process.env.TARGET ?? 'http://localhost:4202/';
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1440, height: 1000 } });
await p.addInitScript((t) => {
  try {
    localStorage.setItem('preferred_language', 'cs');
    localStorage.setItem('cleansia-theme', t);
  } catch { /* private mode */ }
}, THEME);
await p.goto(TARGET, { waitUntil: 'networkidle', timeout: 60000 });
await p.waitForTimeout(2500);
// A wizard's later steps are only reachable through the earlier ones, so a step
// past the first cannot be measured for contrast without walking there first.
const ADVANCE = Number(process.env.ADVANCE ?? 0);
if (ADVANCE > 0) {
  const stopped = await walkWizard(p, ADVANCE, { log: (line) => console.log(line) });
  if (stopped) console.log(`WALK STOPPED EARLY: ${stopped}`);
  await p.waitForTimeout(600);
}

await p.evaluate(async () => { for (let y=0;y<document.body.scrollHeight;y+=600){window.scrollTo(0,y);await new Promise(r=>setTimeout(r,60));} window.scrollTo(0,0); });
await p.waitForTimeout(800);

const rootCls = await p.evaluate(() => document.documentElement.className);
console.log(`theme=${THEME}  <html class="${rootCls}">`);

const rows = await p.evaluate(() => {
  const parse = (c) => { const m = (c||'').match(/[\d.]+/g); if (!m) return null;
    const [r,g,b,a=1] = m.map(Number); return { r, g, b, a }; };
  // Composite translucent layers, and treat a gradient as unsampleable rather
  // than pretending the layer under it is the paint.
  const painted = (el) => {
    let n = el; const stack = []; let gradient = false;
    while (n) { const cs = getComputedStyle(n);
      if (cs.backgroundImage && cs.backgroundImage !== 'none') gradient = true;
      const c = parse(cs.backgroundColor);
      if (c && c.a > 0) { stack.push(c); if (c.a === 1) break; }
      n = n.parentElement; }
    stack.push({ r:255, g:255, b:255, a:1 });
    let out = stack[stack.length - 1];
    for (let i = stack.length - 2; i >= 0; i--) { const f = stack[i];
      out = { r: f.r*f.a + out.r*(1-f.a), g: f.g*f.a + out.g*(1-f.a), b: f.b*f.a + out.b*(1-f.a), a: 1 }; }
    return { bg: out, gradient };
  };
  const lum = (c) => { const f = (v) => { v/=255; return v<=0.03928 ? v/12.92 : Math.pow((v+0.055)/1.055, 2.4); };
    return 0.2126*f(c.r) + 0.7152*f(c.g) + 0.0722*f(c.b); };
  const out = [];
  document.querySelectorAll('h1,h2,h3,h4,p,span,a,li,button,small,strong,div,label').forEach((el) => {
    const t = (el.textContent||'').trim();
    if (!t || t.length > 60 || el.children.length) return;
    // WCAG 1.4.3 exempts an INACTIVE user interface component from the contrast
    // minimum. A disabled control is exactly that, and the exemption exists
    // because dimming is how "you cannot use this" is communicated — reporting
    // it as a failure asks for a disabled control that does not look disabled.
    // Narrow on purpose: it keys off the real disabled state, not off looking
    // faint, so nothing that a visitor can actually use is skipped.
    if (el.disabled === true || el.closest('[disabled], [aria-disabled="true"]')) return;
    const fg = parse(getComputedStyle(el).color); if (!fg || fg.a === 0) return;
    const { bg, gradient } = painted(el);
    if (gradient) return; // cannot sample a gradient; judged by eye instead
    const c = { r: fg.r*fg.a + bg.r*(1-fg.a), g: fg.g*fg.a + bg.g*(1-fg.a), b: fg.b*fg.a + bg.b*(1-fg.a) };
    const l1 = lum(c), l2 = lum(bg);
    const ratio = (Math.max(l1,l2)+0.05)/(Math.min(l1,l2)+0.05);
    const large = parseFloat(getComputedStyle(el).fontSize) >= 18.66 || (parseFloat(getComputedStyle(el).fontSize) >= 14 && +getComputedStyle(el).fontWeight >= 700);
    const floor = large ? 3 : 4.5;
    if (ratio < floor) out.push({ ratio: +ratio.toFixed(2), floor,
      cls: (el.className||'').toString().slice(0,42), txt: t.slice(0,30) });
  });
  return out.sort((a,b) => a.ratio - b.ratio).slice(0, 30);
});
console.log(`${rows.length} text nodes below their WCAG floor`);
for (const r of rows) console.log(`  ${r.ratio.toFixed(2)} (min ${r.floor})  ${r.cls.padEnd(42)} ${r.txt}`);
if (process.env.SHOT) await p.screenshot({ path: process.env.SHOT, fullPage: true });
await b.close();
