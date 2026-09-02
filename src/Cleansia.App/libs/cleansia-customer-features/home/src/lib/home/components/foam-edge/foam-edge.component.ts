import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Which way the bubbles face, and how tall the strip is. */
export type FoamVariant = 'cap' | 'hood' | 'hood-short';

/** One bubble run: how many, how big, how much solid band, and which way it faces. */
interface FoamGeometry {
  /** Bubbles across the 1440 viewBox. */
  readonly count: number;
  /** Bubble radius. The chord is 2r, so `count * 2r` must be exactly 1440. */
  readonly radius: number;
  /** Solid band behind the bubbles, measured from the strip's filled edge. */
  readonly band: number;
  /** `true` fills the bottom and arcs bubbles up; `false` fills the top and hangs them down. */
  readonly up: boolean;
  /** viewBox height. A bubble taller than `strip - band` is cropped, deliberately or not. */
  readonly strip: number;
}

/**
 * The scalloped divider between sections — the suds motif the mobile apps
 * already use for pull-to-refresh (`SudsRefreshIndicator`), carried onto the web
 * as structure rather than decoration.
 *
 * Two shapes, not one shape flipped. A `cap` fills the BOTTOM of its strip and
 * arcs bubbles upward into the section above; a `hood` fills the TOP and hangs
 * bubbles downward into the section below. `scaleY(-1)` on a cap does not produce
 * a hood — it produces a cap pointing the wrong way, with the fill on the wrong
 * side of the line, which is how the divider under the gallery came out as a
 * band of blue rectangles.
 *
 * ## Cropping is a design choice here, not an accident
 *
 * SVG does not scale an arc that does not fit its viewBox — it draws it and the
 * box crops the overflow. `cap` and `hood` are sized so nothing is cropped and
 * the bubbles read as full rounds. `hood-short` deliberately is not: a 60-radius
 * bubble off a 28 band in a 60-tall strip has its far edge cut away, which is
 * what gives that variant its flatter, squarer tabs. Owner ruling 2026-09-02,
 * after seeing both — the two shapes are the vocabulary, and the short one is
 * the square sibling of the round one.
 *
 * The one thing that was genuinely broken is fixed and stays fixed: `hood` used
 * the sweep flag that bulges its arcs UP into its own band rather than down out
 * of it, so sixty units of bubble ate a thirty-two unit band and all that
 * rendered was the slivers left at the cusps — a row of thin spikes.
 *
 * There is no second path and no rim colour. The bubbles are drawn in the fill
 * the caller passes and nothing else; an outline made the divider read as a
 * graphic rather than as foam. Owner, 2026-09-02: "revert to the one that was
 * in the past".
 */
const GEOMETRY: Record<FoamVariant, FoamGeometry> = {
  // The hero's edge. 30 + 60 = 90: whole bubbles.
  cap: { count: 12, radius: 60, band: 30, up: true, strip: 90 },
  // The same bubble, hung the other way, in a strip tall enough to hold it.
  hood: { count: 12, radius: 60, band: 30, up: false, strip: 90 },
  // The square sibling, hung the same way as `hood`. 28 + 60 against a 60 strip
  // crops each bubble's far edge by 28, which is the flatter tab — not an
  // oversight. Owner ruling 2026-09-02: the home page carries the round shape
  // throughout and the services page carries this one, so the two pages read
  // as the same motif at two weights rather than as one page disagreeing with
  // itself.
  'hood-short': { count: 12, radius: 60, band: 28, up: false, strip: 60 },
};

@Component({
  selector: 'cleansia-foam-edge',
  templateUrl: './foam-edge.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FoamEdgeComponent {
  /** Colour the bubbles are cut out of — the section the edge leads into. */
  readonly fill = input<string>('var(--cl-foam, var(--surface-card))');

  /**
   * `cap` is the 90-tall edge that arcs up, `hood` the 90-tall one that hangs
   * down, `hood-short` its 60-tall square sibling. The home page uses the round
   * pair throughout; the services page uses the square one.
   */
  readonly variant = input<FoamVariant>('cap');

  private readonly geometry = computed(() => GEOMETRY[this.variant()]);

  readonly height = computed(() => this.geometry().strip);

  readonly path = computed(() => {
    const { count, radius, band, up, strip } = this.geometry();
    const chord = radius * 2;

    // Sweep 1 travelling right bulges up; sweep 1 travelling left bulges down.
    // The run is written once here rather than per section — the last time it
    // was repeated by hand the copies drifted apart.
    if (up) {
      const arcs = ` a${radius},${radius} 0 0 1 ${chord},0`.repeat(count);
      return `M0,${strip} L0,${strip - band}${arcs} L1440,${strip} Z`;
    }
    const arcs = ` a${radius},${radius} 0 0 1 -${chord},0`.repeat(count);
    return `M0,0 L1440,0 L1440,${band}${arcs} Z`;
  });
}
