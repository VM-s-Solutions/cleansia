import { chromium } from 'playwright';
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1440, height: 1000 } });
await p.goto('http://localhost:4202/', { waitUntil: 'networkidle', timeout: 60000 });
await p.waitForTimeout(2000);
await p.evaluate(async () => {
  for (let y = 0; y < document.body.scrollHeight; y += 700) { window.scrollTo(0, y); await new Promise(r => setTimeout(r, 100)); }
  window.scrollTo(0, 0);
});
await p.waitForTimeout(1000);
const rows = await p.evaluate(() =>
  [...document.querySelectorAll('.cl-section--rules, .cl-section--plus, .cl-section--faq, .cl-cta')].flatMap((sec) => {
    const kids = [sec, ...sec.querySelectorAll('.animate-on-scroll, .cl-section__inner, .cl-cta__card')];
    return kids.slice(0, 4).map((el) => {
      const cs = getComputedStyle(el);
      const r = el.getBoundingClientRect();
      return { cls: (el.className||'').toString().slice(0,34), op: cs.opacity, vis: cs.visibility,
               cv: cs.contentVisibility, tr: cs.transform.slice(0,22), top: Math.round(r.top), h: Math.round(r.height) };
    });
  }));
console.table(rows);
await b.close();
