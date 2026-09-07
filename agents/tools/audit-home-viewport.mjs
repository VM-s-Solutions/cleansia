/**
 * Measure one viewport x one locale of the customer home page and report every
 * geometric defect a reader would see.
 *
 * Deterministic on purpose: "the mobile version is broken" is not actionable,
 * and a screenshot at one scroll offset hides most of it. This reports what is
 * true regardless of who is looking - horizontal overflow, a child escaping its
 * parent, clipped text, overlapping controls, tap targets under the 40px floor,
 * and images that never loaded.
 *
 *   WIDTH=390 HEIGHT=844 LANG_CODE=cs node agents/tools/audit-home-viewport.mjs
 *
 * Run it from src/Cleansia.App - that is where playwright is installed.
 */
import { createRequire } from 'node:module';
const { chromium } = createRequire(`${process.cwd()}/`)('playwright');
import { walkWizard } from './wizard-walk.mjs';

const WIDTH = Number(process.env.WIDTH ?? 390);
const HEIGHT = Number(process.env.HEIGHT ?? 844);
const LANG = process.env.LANG_CODE ?? 'cs';
const THEME = process.env.THEME ?? 'light';
const TARGET = process.env.TARGET ?? 'http://localhost:4202/';
const SHOT = process.env.SHOT ?? '';

const browser = await chromium.launch();
const page = await browser.newPage({
  viewport: { width: WIDTH, height: HEIGHT },
  deviceScaleFactor: 1,
  isMobile: WIDTH < 800,
  hasTouch: WIDTH < 800,
});
await page.addInitScript((cfg) => {
  try {
    localStorage.setItem('preferred_language', cfg.lang);
    localStorage.setItem('cleansia-theme', cfg.theme);
    // The banner is fixed to the bottom and would overlap whatever it sits
    // over, drowning the real findings. Key and value shape come from
    // apps/cleansia.app/src/app/app.html.
    localStorage.setItem('cleansia-customer-cookie-consent', 'accepted');
    localStorage.setItem(
      'cleansia-customer-cookie-consent-preferences',
      JSON.stringify({ necessary: true, analytics: true, marketing: true })
    );
  } catch {
    /* private mode */
  }
}, { lang: LANG, theme: THEME });

const consoleErrors = [];
page.on('console', (m) => {
  if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 200));
});

await page.goto(TARGET, { waitUntil: 'networkidle', timeout: 90000 });

// A wizard's later steps are only reachable through the earlier ones. ADVANCE=n
// walks forward the way a visitor does, so a step past the first can be audited
// at all — without it every run measures step one and reports it clean.
const ADVANCE = Number(process.env.ADVANCE ?? 0);
if (ADVANCE > 0) {
  const stopped = await walkWizard(page, ADVANCE, { log: (line) => console.log(line) });
  if (stopped) console.log(`WALK STOPPED EARLY: ${stopped}`);
  await page.waitForTimeout(600);
}

// Everything below the fold has to be laid out before it can be measured.
await page.evaluate(async () => {
  for (let y = 0; y < document.body.scrollHeight; y += 400) {
    window.scrollTo(0, y);
    await new Promise((r) => setTimeout(r, 40));
  }
  window.scrollTo(0, 0);
});
await page.waitForTimeout(900);

const report = await page.evaluate((vw) => {
  const path = (el) => {
    const bits = [];
    for (let e = el; e && e.tagName && bits.length < 4; e = e.parentElement) {
      const cls = (e.className || '')
        .toString()
        .split(/\s+/)
        .filter((c) => /^(cl-|customer-|cleansia)/.test(c))
        .slice(0, 2)
        .join('.');
      bits.unshift(e.tagName.toLowerCase() + (cls ? '.' + cls : ''));
    }
    return bits.join(' > ');
  };
  const visible = (el) => {
    const cs = getComputedStyle(el);
    if (cs.display === 'none' || cs.visibility === 'hidden' || cs.opacity === '0') return false;
    const r = el.getBoundingClientRect();
    return r.width > 0 && r.height > 0;
  };

  const all = [...document.querySelectorAll('body *')].filter(visible);

  // A strip the user can scroll sideways on purpose - a step rail, a chip row -
  // holds children past the viewport BY DESIGN. They are only a defect if the
  // PAGE scrolls, which is measured separately.
  const inScroller = (el) => {
    for (let e = el.parentElement; e && e !== document.body; e = e.parentElement) {
      const ov = getComputedStyle(e).overflowX;
      if (ov === 'auto' || ov === 'scroll') return true;
    }
    return false;
  };

  // 1. Anything wider than the viewport, or starting left of it.
  const overflowNodes = [];
  for (const el of all) {
    const r = el.getBoundingClientRect();
    const right = r.left + r.width;
    if ((right > vw + 1.5 || r.left < -1.5) && !inScroller(el)) {
      // Only the OUTERMOST offender; a child of an overflowing box adds nothing.
      if (!overflowNodes.some((o) => o.el.contains(el))) {
        overflowNodes.push({
          el,
          sel: path(el),
          left: Math.round(r.left),
          right: Math.round(right),
          w: Math.round(r.width),
        });
      }
    }
  }

  // 2. Text clipped by its own box.
  //
  // Not counting the sr-only pattern: a label that names an input for a screen
  // reader while the caller prints its own heading is clipped ON PURPOSE, and
  // its whole signature — a 1px box with clip/clip-path — is how that is done.
  // Reporting it makes a real clip indistinguishable from a deliberate one, and
  // a checker with a permanent known-false line stops being read.
  const srOnly = (el, cs) => {
    const r = el.getBoundingClientRect();
    const clipsAway = cs.clipPath === 'inset(50%)' || /rect\(0px,? 0px,? 0px,? 0px\)/.test(cs.clip);
    return clipsAway && r.width <= 2 && r.height <= 2;
  };

  const clipped = [];
  for (const el of all) {
    if (!el.childNodes.length) continue;
    const hasText = [...el.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim());
    if (!hasText) continue;
    const cs = getComputedStyle(el);
    if (srOnly(el, cs)) continue;
    const hidden = /hidden|clip/.test(cs.overflow + cs.overflowX + cs.overflowY);
    if (!hidden) continue;
    if (el.scrollWidth > el.clientWidth + 2) {
      clipped.push({
        sel: path(el),
        text: el.textContent.trim().slice(0, 48),
        axis: 'x',
        scroll: el.scrollWidth,
        client: el.clientWidth,
      });
    } else if (el.scrollHeight > el.clientHeight + 2 && cs.overflowY !== 'auto' && cs.overflowY !== 'scroll') {
      clipped.push({
        sel: path(el),
        text: el.textContent.trim().slice(0, 48),
        axis: 'y',
        scroll: el.scrollHeight,
        client: el.clientHeight,
      });
    }
  }

  // 3. Interactive targets under the 40px floor.
  const small = [];
  const controlSel = 'a[href], button, [role="button"], input, select, .cl-chip, .p-select';
  const controls = document.querySelectorAll(controlSel);
  for (const el of controls) {
    if (!visible(el)) continue;
    // A chevron inside a 52px picker, or a checkbox inside its own label, is
    // not its own target - the thing wrapping it is. Only report a control
    // that is not enclosed by a larger control, or by a label.
    const wrapper = el.parentElement && el.parentElement.closest(controlSel + ', label');
    if (wrapper && wrapper !== el) {
      const wr = wrapper.getBoundingClientRect();
      if (wr.height >= 40) continue;
    }
    const r = el.getBoundingClientRect();
    if (r.height < 40 || r.width < 24) {
      small.push({
        sel: path(el),
        text: (el.textContent || '').trim().slice(0, 30),
        w: Math.round(r.width),
        h: Math.round(r.height),
      });
    }
  }

  // 4. Overlapping interactive elements - a real click-blocker.
  //
  // Compared FRAGMENT by fragment, not by bounding box. An inline link that
  // wraps onto a second line has a bounding box spanning both lines and the
  // full column width, so any other link on either line intersects it — two
  // links in one sentence of terms copy reported as a click-blocker on every
  // phone width. getClientRects() gives the boxes actually painted.
  const inter = [...document.querySelectorAll('a[href], button')]
    .filter(visible)
    .map((e) => ({ e, rects: [...e.getClientRects()] }));
  const overlaps = [];
  for (let i = 0; i < inter.length; i++) {
    for (let j = i + 1; j < inter.length; j++) {
      const a = inter[i];
      const b = inter[j];
      if (a.e.contains(b.e) || b.e.contains(a.e)) continue;
      let hit = null;
      for (const ra of a.rects) {
        for (const rb of b.rects) {
          const ox = Math.min(ra.right, rb.right) - Math.max(ra.left, rb.left);
          const oy = Math.min(ra.bottom, rb.bottom) - Math.max(ra.top, rb.top);
          if (ox > 4 && oy > 4) hit = { ox: Math.round(ox), oy: Math.round(oy) };
        }
      }
      if (hit) overlaps.push({ a: path(a.e), b: path(b.e), ...hit });
    }
  }

  // 5. Images that did not load, or render at zero.
  //
  // An image with NO layout box at all is not broken — it is hidden, which is
  // what every decorative mascot is below the tablet breakpoint. Flagging those
  // buried the real failures under a row of false positives on every phone
  // width, which is the way a checker stops being read.
  const badImgs = [];
  for (const img of document.querySelectorAll('img')) {
    if (img.getClientRects().length === 0) continue;
    const r = img.getBoundingClientRect();
    const name = (img.currentSrc || img.src || '').split('/').pop();
    if (!img.complete || img.naturalWidth === 0) badImgs.push({ sel: path(img), src: name, reason: 'not loaded' });
    else if (r.width < 2 || r.height < 2)
      badImgs.push({ sel: path(img), src: name, reason: `renders ${Math.round(r.width)}x${Math.round(r.height)}` });
  }

  // 6. A child escaping its parent's box horizontally.
  const escapedNodes = [];
  for (const el of all) {
    const p = el.parentElement;
    if (!p || p === document.body || p === document.documentElement) continue;
    if (getComputedStyle(p).overflow !== 'visible') continue;
    const ecs = getComputedStyle(el);
    if (ecs.position === 'absolute' || ecs.position === 'fixed') continue;
    // A NEGATIVE margin is a deliberate device, not an accident - the hero
    // mascot is nudged 18px left of its column on purpose, as the artboard
    // draws it. Only report an element that escapes by MORE than it was
    // deliberately pulled.
    const pull = Math.max(
      -parseFloat(ecs.marginLeft) || 0,
      -parseFloat(ecs.marginRight) || 0
    );
    const r = el.getBoundingClientRect();
    const pr = p.getBoundingClientRect();
    if (pr.width < 8) continue;
    const by = Math.max(pr.left - r.left, r.right - pr.right);
    if (by > 2 + pull) {
      if (!escapedNodes.some((x) => x.el.contains(el))) {
        escapedNodes.push({ el, sel: path(el), by: Math.round(by), deliberatePull: Math.round(pull) });
      }
    }
  }

  const sections = [...document.querySelectorAll('main > *')].map((s) => {
    const r = s.getBoundingClientRect();
    return { tag: s.tagName.toLowerCase(), h: Math.round(r.height) };
  });

  const displayOf = (sel) => {
    const e = document.querySelector(sel);
    return e ? getComputedStyle(e).display : 'missing';
  };

  return {
    docScrollWidth: document.documentElement.scrollWidth,
    pageHeight: document.body.scrollHeight,
    overflowX: overflowNodes.map(({ sel, left, right, w }) => ({ sel, left, right, w })),
    clipped: clipped.slice(0, 25),
    small: small.slice(0, 30),
    overlaps: overlaps.slice(0, 20),
    badImgs,
    escaped: escapedNodes.map(({ sel, by }) => ({ sel, by })).slice(0, 25),
    sections,
    navDesktop: displayOf('.customer-navbar__center'),
    navRight: displayOf('.customer-navbar__right'),
    navHamburger: displayOf('.customer-navbar__hamburger'),
  };
}, WIDTH);

console.log(`=== ${WIDTH}x${HEIGHT} lang=${LANG} theme=${THEME} ===`);
console.log(`page height ${report.pageHeight}px | doc scrollWidth ${report.docScrollWidth} (viewport ${WIDTH})`);
const over = report.docScrollWidth - WIDTH;
console.log(`HORIZONTAL SCROLL: ${over > 1 ? 'YES - ' + over + 'px' : 'no'}`);
console.log(`nav: center=${report.navDesktop} right=${report.navRight} hamburger=${report.navHamburger}`);

const dump = (name, arr) => {
  console.log(`\n${name}: ${arr.length}`);
  arr.forEach((x) => console.log('   ' + JSON.stringify(x)));
};
dump('OVERFLOWING VIEWPORT', report.overflowX);
dump('ESCAPING PARENT', report.escaped);
dump('CLIPPED TEXT', report.clipped);
// The 40px floor is a TOUCH rule. On a desktop width these are mouse targets -
// a 20px nav link is normal there - so they are listed for information and the
// heading says which it is.
dump(WIDTH < 900 ? 'TAP TARGETS UNDER 40px' : 'small targets (mouse context, informational)', report.small);
dump('OVERLAPPING CONTROLS', report.overlaps);
dump('BROKEN IMAGES', report.badImgs);
console.log('\nsections: ' + report.sections.map((s) => `${s.tag}:${s.h}`).join(' '));
console.log('console errors: ' + consoleErrors.length);
consoleErrors.slice(0, 6).forEach((e) => console.log('   ' + e));

if (SHOT) {
  await page.screenshot({ path: SHOT, fullPage: true });
  console.log('shot: ' + SHOT);
}
await browser.close();
