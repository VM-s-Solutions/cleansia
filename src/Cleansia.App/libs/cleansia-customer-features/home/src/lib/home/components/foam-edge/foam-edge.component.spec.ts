import { TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { FoamEdgeComponent, FoamVariant } from './foam-edge.component';

/**
 * The arithmetic that broke silently.
 *
 * All three variants hard-coded a 60 radius, and only the 90-tall `cap` had
 * room for it. SVG does not scale an arc that does not fit — it draws it and
 * the viewBox crops whatever hangs outside — so `hood` rendered as a row of
 * thin spikes and `cap-short` as shallow humps, on six call sites and in the
 * approved artboard, for as long as they existed. Nothing failed; it just did
 * not look like foam.
 *
 * These tests read the path back and check it fits the strip it is drawn in.
 */
describe('FoamEdgeComponent', () => {
  const VARIANTS: FoamVariant[] = ['cap', 'cap-short', 'hood'];

  function build(variant: FoamVariant): {
    ref: ComponentRef<FoamEdgeComponent>;
    component: FoamEdgeComponent;
  } {
    const fixture = TestBed.createComponent(FoamEdgeComponent);
    fixture.componentRef.setInput('variant', variant);
    fixture.detectChanges();
    return { ref: fixture.componentRef, component: fixture.componentInstance };
  }

  /**
   * Every arc in the run, as {radius, dx, sweep}. The component writes them as
   * `a{r},{r} 0 0 {sweep} {dx},0`, so a chord of `dx` on a radius of `r` is a
   * half-circle whose apex is `r` from the baseline.
   */
  function arcs(path: string): { radius: number; dx: number; sweep: number }[] {
    const out: { radius: number; dx: number; sweep: number }[] = [];
    const re = /a(\d+),\d+ 0 0 (\d) (-?\d+),0/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(path)) !== null) {
      out.push({ radius: Number(m[1]), sweep: Number(m[2]), dx: Number(m[3]) });
    }
    return out;
  }

  /** The y the run starts from — the baseline the bubbles bulge away from. */
  function baseline(path: string): number {
    const cap = /^M0,\d+ L0,(\d+)/.exec(path);
    if (cap) return Number(cap[1]);
    const hood = /^M0,0 L1440,0 L1440,(\d+)/.exec(path);
    if (!hood) throw new Error(`unrecognised path: ${path}`);
    return Number(hood[1]);
  }

  describe.each(VARIANTS)('%s', (variant) => {
    it('draws a run that spans the canvas exactly', () => {
      const { component } = build(variant);
      const run = arcs(component.path());

      expect(run.length).toBeGreaterThan(0);
      expect(run.reduce((sum, a) => sum + Math.abs(a.dx), 0)).toBe(1440);
    });

    it('uses a chord of exactly two radii, so every bubble is a half-circle', () => {
      const { component } = build(variant);

      for (const a of arcs(component.path())) {
        expect(Math.abs(a.dx)).toBe(a.radius * 2);
      }
    });

    it('keeps every bubble inside the strip', () => {
      // The bug: a 60-radius bubble off a 32 baseline needs 92 units of strip
      // and had 60, so 32 units of every bubble were cropped away.
      const { component } = build(variant);
      const path = component.path();
      const base = baseline(path);
      const run = arcs(path);
      const apex = run[0].dx > 0 ? base - run[0].radius : base + run[0].radius;

      expect(apex).toBeGreaterThanOrEqual(0);
      expect(apex).toBeLessThanOrEqual(component.height());
    });

    it('puts the rim crescent on the outside of the bubbles', () => {
      // Offset the other way and the crescent hides behind the foam, which is
      // the whole reason the edge gets a value of its own.
      const { component } = build(variant);
      const run = arcs(component.path());
      const bulgesUp = run[0].dx > 0;

      expect(component.rimOffset()).not.toBe(0);
      expect(component.rimOffset() < 0).toBe(bulgesUp);
    });
  });

  it('hangs a hood down and arcs a cap up', () => {
    // A hood is not a flipped cap. If these ever agree, one of them is drawing
    // its fill on the wrong side of the line.
    const hood = build('hood').component;
    const cap = build('cap').component;

    expect(arcs(hood.path())[0].dx).toBeLessThan(0);
    expect(arcs(cap.path())[0].dx).toBeGreaterThan(0);
  });

  it('gives the shorter strip a smaller bubble rather than a cropped one', () => {
    const short = build('cap-short').component;
    const cap = build('cap').component;

    expect(short.height()).toBeLessThan(cap.height());
    expect(arcs(short.path())[0].radius).toBeLessThan(arcs(cap.path())[0].radius);
  });

  it('lets the caller name the colour the bubbles are cut out of', () => {
    // It has to equal the section the edge leads into, or a hard band appears.
    const { ref, component } = build('hood');
    ref.setInput('fill', 'var(--cl-cta-3)');

    expect(component.fill()).toBe('var(--cl-cta-3)');
  });
});
