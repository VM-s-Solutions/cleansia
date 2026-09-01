/**
 * Does the price calculator hold still while it recalculates?
 *
 * The result row used to swap a two-line price block for a one-line status
 * whenever a quote was in flight, so the card changed height on every chip tap.
 * That reads as the calculator dragging rather than calculating, and it is
 * invisible in a screenshot - it only exists between two frames.
 *
 * This taps through the size chips and samples the card's geometry every frame
 * of the request, reporting the largest movement it sees.
 *
 * Run from src/Cleansia.App.
 */
import { createRequire } from 'node:module';
const { chromium } = createRequire(`${process.cwd()}/`)('playwright');

const TARGET = process.env.TARGET ?? 'http://localhost:4202/';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
await page.addInitScript(() => {
  try {
    localStorage.setItem('preferred_language', 'cs');
    localStorage.setItem('cleansia-customer-cookie-consent', 'accepted');
  } catch {
    /* private mode */
  }
});
await page.goto(TARGET, { waitUntil: 'networkidle' });
await page.waitForSelector('.cl-quote__price');
await page.waitForTimeout(800);

// Watch the card and the result row for the whole interaction, sampling on
// every animation frame rather than polling - a one-frame jump still counts.
await page.evaluate(() => {
  window.__samples = [];
  const tick = () => {
    const card = document.querySelector('.cl-quote');
    const row = document.querySelector('.cl-quote__result');
    const price = document.querySelector('.cl-quote__price');
    if (card && row && price) {
      window.__samples.push({
        card: Math.round(card.getBoundingClientRect().height),
        row: Math.round(row.getBoundingClientRect().height),
        price: Math.round(price.getBoundingClientRect().height),
        top: Math.round(row.getBoundingClientRect().top),
      });
    }
    window.__raf = requestAnimationFrame(tick);
  };
  tick();
});

const sizeChips = page.locator('.cl-quote__field .cl-quote__chips:not(.cl-quote__chips--services) .cl-chip');
const n = await sizeChips.count();
console.log(`size chips: ${n}`);
for (let i = 0; i < Math.min(n, 5); i++) {
  await sizeChips.nth(i).click();
  await page.waitForTimeout(700);
}
// And a couple of service changes, which also refetch.
const svcChips = page.locator('.cl-quote__chips--services .cl-chip');
const sn = await svcChips.count();
for (let i = 0; i < Math.min(sn, 3); i++) {
  await svcChips.nth(i).click();
  await page.waitForTimeout(700);
}
await page.waitForTimeout(600);

const result = await page.evaluate(() => {
  cancelAnimationFrame(window.__raf);
  const s = window.__samples;
  const span = (k) => {
    const vals = s.map((x) => x[k]);
    return { min: Math.min(...vals), max: Math.max(...vals), delta: Math.max(...vals) - Math.min(...vals) };
  };
  return { frames: s.length, card: span('card'), row: span('row'), price: span('price'), top: span('top') };
});

console.log(`frames sampled: ${result.frames}`);
for (const k of ['card', 'row', 'price', 'top']) {
  const v = result[k];
  console.log(`  ${k.padEnd(6)} ${String(v.min).padStart(5)} .. ${String(v.max).padStart(5)}  moved ${v.delta}px`);
}

// A couple of pixels is font rendering; anything more is a jump the reader sees.
const worst = Math.max(result.card.delta, result.row.delta, result.price.delta, result.top.delta);
console.log(worst <= 2 ? '\nthe calculator holds still while it recalculates' : `\nCALCULATOR MOVES: ${worst}px during recalculation`);

await browser.close();
process.exit(worst <= 2 ? 0 : 1);
