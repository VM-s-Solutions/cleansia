/**
 * Does the customer nav bar fit inside its pill in EVERY locale?
 *
 * The bar is a fixed-width pill holding a brand, four links, two controls, a
 * sign-in link and a CTA. Czech needs 1195px of viewport; Ukrainian needs 1331.
 * The breakpoint was set from Czech, so switching to Ukrainian pushed the
 * right-hand cluster 105px past the pill - which is what "the layout breaks
 * when I translate" turned out to be.
 *
 * This measures the intrinsic width of the bar's three groups in each locale
 * and fails if any needs more than the breakpoint the app actually uses. A
 * longer translation therefore fails a check instead of shipping broken.
 *
 * Run from src/Cleansia.App.
 */
import { createRequire } from 'node:module';
const { chromium } = createRequire(`${process.cwd()}/`)('playwright');

const TARGET = process.env.TARGET ?? 'http://localhost:4202/';
const LOCALES = (process.env.LOCALES ?? 'cs,en,ru,sk,uk').split(',');

// Keep in step with NAV_DESKTOP_MIN_WIDTH in customer-navbar.component.ts and
// the media query in cleansia-customer-navbar.component.scss.
const BREAKPOINT = Number(process.env.BREAKPOINT ?? 1360);
// The pill sits in 40px page margins, and the three groups need real space
// between them or the bar reads as one run-on strip.
const PAGE_MARGINS = 80;
const GROUP_GUTTERS = 80;

const browser = await chromium.launch();
let worst = 0;
let failures = 0;

for (const locale of LOCALES) {
  const page = await browser.newPage({ viewport: { width: 1920, height: 900 } });
  await page.addInitScript((l) => {
    try {
      localStorage.setItem('preferred_language', l);
      localStorage.setItem('cleansia-customer-cookie-consent', 'accepted');
    } catch {
      /* private mode */
    }
  }, locale);
  await page.goto(TARGET, { waitUntil: 'networkidle', timeout: 90000 });
  await page.waitForSelector('.customer-navbar__center');
  await page.waitForTimeout(700);

  const m = await page.evaluate(() => {
    // Sum each group's children at their natural width. The laid-out width is
    // useless here: the groups flex-grow under `justify-content: space-between`,
    // so all three report the same inflated number at a wide viewport.
    const sum = (sel) => {
      const g = document.querySelector(sel);
      if (!g) return 0;
      const kids = [...g.children].filter((c) => getComputedStyle(c).display !== 'none');
      const gap = parseFloat(getComputedStyle(g).columnGap) || 0;
      const w = kids.reduce((a, c) => a + c.getBoundingClientRect().width, 0);
      return Math.ceil(w + gap * Math.max(0, kids.length - 1));
    };
    const inner = document.querySelector('.customer-navbar__inner');
    const cs = getComputedStyle(inner);
    return {
      left: sum('.customer-navbar__left'),
      center: sum('.customer-navbar__center'),
      right: sum('.customer-navbar__right'),
      pad: parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight),
      links: [...document.querySelectorAll('.customer-navbar__center a')].map((a) => a.textContent.trim()),
    };
  });

  const needed = Math.ceil(m.left + m.center + m.right + m.pad + PAGE_MARGINS + GROUP_GUTTERS);
  worst = Math.max(worst, needed);
  const ok = needed <= BREAKPOINT;
  if (!ok) failures++;
  console.log(
    `  ${ok ? 'ok  ' : 'FAIL'} ${locale}  left=${String(m.left).padStart(4)} center=${String(m.center).padStart(4)} right=${String(m.right).padStart(4)}  needs ${needed}px (breakpoint ${BREAKPOINT})`
  );
  if (!ok) console.log(`        links: ${m.links.join(' | ')}`);
  await page.close();
}

console.log(
  failures
    ? `\n${failures} locale(s) need more than the ${BREAKPOINT}px breakpoint — widest is ${worst}px`
    : `\nevery locale fits the pill; widest is ${worst}px against a ${BREAKPOINT}px breakpoint`
);
await browser.close();
process.exit(failures ? 1 : 0);
