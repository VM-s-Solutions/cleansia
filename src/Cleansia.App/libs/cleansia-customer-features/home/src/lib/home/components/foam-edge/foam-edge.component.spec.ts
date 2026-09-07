import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { FoamEdgeComponent, FoamVariant } from './foam-edge.component';

/**
 * What the divider must keep doing.
 *
 * The one thing that was genuinely broken: `hood` used the sweep flag that
 * bulges its arcs UP into its own band rather than down out of it, so sixty
 * units of bubble ate a thirty-two unit band and all that rendered was the
 * slivers left at the cusps — a row of thin spikes rather than foam, on four of
 * the six call sites and in the approved artboard. Nothing failed; it just did
 * not look like the design.
 *
 * `cap-short`'s crop is NOT that bug. It is cropped on purpose, and by a known
 * amount, because the flatter tab is the square sibling of the round bubble and
 * both are part of the vocabulary. The test below pins the amount so the two
 * cases stay distinguishable: an intentional crop that drifts is a bug again.
 */
describe('FoamEdgeComponent', () => {
  const VARIANTS: FoamVariant[] = ['cap', 'cap-short', 'hood', 'hood-short'];

  function build(variant: FoamVariant): {
    fixture: ComponentFixture<FoamEdgeComponent>;
    ref: ComponentRef<FoamEdgeComponent>;
    component: FoamEdgeComponent;
  } {
    const fixture = TestBed.createComponent(FoamEdgeComponent);
    fixture.componentRef.setInput('variant', variant);
    fixture.detectChanges();
    return { fixture, ref: fixture.componentRef, component: fixture.componentInstance };
  }

  /**
   * Every arc in the run. The component writes them as `a{r},{r} 0 0 {sweep}
   * {dx},0`, so a chord of `dx` on a radius of `r` is a half-circle whose apex
   * is `r` from the baseline.
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

  /** How far the bubbles' far edge falls outside the strip. 0 means uncropped. */
  function overhang(component: FoamEdgeComponent): number {
    const path = component.path();
    const run = arcs(path);
    const apex = run[0].dx > 0 ? baseline(path) - run[0].radius : baseline(path) + run[0].radius;
    return run[0].dx > 0 ? Math.max(0, -apex) : Math.max(0, apex - component.height());
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
  });

  it('leaves the round bubbles whole', () => {
    expect(overhang(build('cap').component)).toBe(0);
    expect(overhang(build('hood').component)).toBe(0);
  });

  it('crops both square siblings by exactly the amount that flattens them', () => {
    // 60 of bubble off a 28 band in a 60 strip. Change any of the three and the
    // tab stops being the shape the design asked for. This is the divider every
    // call site draws, so it is the one worth pinning hardest.
    expect(overhang(build('cap-short').component)).toBe(28);
    expect(overhang(build('hood-short').component)).toBe(28);
  });

  it('hangs a hood down and arcs a cap up', () => {
    // A hood is not a flipped cap. This is the one that was actually broken:
    // the hood's arcs bulged back into its own band and left only spikes.
    const hood = build('hood').component;
    const cap = build('cap').component;

    expect(arcs(hood.path())[0].dx).toBeLessThan(0);
    expect(arcs(cap.path())[0].dx).toBeGreaterThan(0);
    expect(overhang(hood)).toBe(0);
  });

  it('draws the bubbles in one colour and nothing else', () => {
    // No second path and no rim: an outline made the divider read as a graphic
    // rather than as foam. Owner, 2026-09-02.
    //
    // Asserted on the RENDERED SVG. This read `Object.keys(component)` for an absent
    // `rimOffset`, which is a claim about the instance's property names and cannot see a second
    // <path> in the template at all — the only place a rim could actually come back.
    const { fixture } = build('hood');
    const host = fixture.nativeElement as HTMLElement;

    expect(host.querySelectorAll('path')).toHaveLength(1);
    expect(host.querySelector('path')!.getAttribute('stroke')).toBeNull();
  });

  it('lets the caller name the colour the bubbles are cut out of', () => {
    // It has to equal the section the edge leads into, or a hard band appears.
    //
    // Asserted on the path's attribute, not on `component.fill()`: reading the input signal back
    // after setInput only proves Angular stored it, and would still pass if the template stopped
    // binding [attr.fill] entirely — which is the whole failure this test is named for.
    const { fixture, ref } = build('hood');
    ref.setInput('fill', 'var(--cl-cta-3)');
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('path')!.getAttribute('fill'),
    ).toBe('var(--cl-cta-3)');
  });
});
