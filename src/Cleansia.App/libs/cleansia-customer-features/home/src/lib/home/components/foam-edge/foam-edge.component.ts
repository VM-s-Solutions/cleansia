import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Which way the bubbles face, and how tall the strip is. */
export type FoamVariant = 'cap' | 'cap-short' | 'hood';

/** One bubble run: how many, how big, and how much solid band sits behind them. */
interface FoamGeometry {
  /** Bubbles across the 1440 viewBox. */
  readonly count: number;
  /** Bubble radius. The chord is 2r, so `count * 2r` must be exactly 1440. */
  readonly radius: number;
  /** Solid band behind the bubbles. The strip is `band + radius + RIM` tall. */
  readonly band: number;
  /** `1` fills the bottom and arcs bubbles up; `0` fills the top and hangs them down. */
  readonly up: boolean;
}

/**
 * How much clear strip is left on the OUTSIDE of the bubbles, for the crescent.
 *
 * Without it the crescent is cropped exactly where the bubbles touch the edge
 * of the viewBox — which is each bump's crest, the part that carries the shape.
 * On a `cap` that left only the slivers between bumps, so the edge rendered as a
 * row of blue spikes hanging into the section instead of a line following the
 * scallops, and the caps and hoods on the same page disagreed about what the
 * motif even looks like. Owner, 2026-09-02.
 */
const RIM = 10;

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
 * ## The invariant, and why it is a table
 *
 * A run of half-circles only renders as bubbles when the strip is exactly as
 * tall as the band plus the radius. Break that and SVG does not scale the arc
 * to fit — it draws it anyway and the viewBox crops whatever hangs outside:
 *
 *   - `hood` ran twelve 60-radius arcs off a 32 band in a 60-tall strip, with
 *     the sweep that bulges them UP rather than down. Sixty units of bubble ate
 *     the whole 32-unit band and everything above it, so what actually rendered
 *     was the slivers left at the cusps: a row of thin spikes, not foam.
 *   - `cap-short` had the right sweep but the same arithmetic problem — 60 of
 *     bubble against a 28 band — so its bubbles came out as shallow humps with
 *     notches bitten out of them.
 *   - `cap` was the only one that added up (30 + 60 = 90) and the only one that
 *     ever looked right, which is why nobody suspected the other two.
 *
 * Owner, 2026-09-02: "there is a pattern which is not always visible, can you
 * change it so that it's better visible". Both halves of that are fixed here —
 * the shape now draws as designed, and `--cl-foam-rim` gives the seam a value
 * of its own so it no longer depends on the two grounds differing. Measured at
 * the six call sites, the edge sat between 1.001 and 1.26 against the band
 * behind it; 1.00 is invisible by definition.
 *
 * The geometry lives in one table so the three variants cannot drift apart
 * again the way they did when each was a hand-written path string.
 */
const GEOMETRY: Record<FoamVariant, FoamGeometry> = {
  // The hero's edge.
  cap: { count: 12, radius: 60, band: 30, up: true },
  // The same bubble, hung the other way.
  hood: { count: 12, radius: 60, band: 30, up: false },
  // Genuinely shorter, which means a smaller bubble rather than a cropped one:
  // half the radius, twice as many.
  'cap-short': { count: 24, radius: 30, band: 30, up: true },
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
   * `cap` is the hero's 90-tall edge, `hood` the 90-tall edge that opens a
   * tinted band, `cap-short` the gallery's 60-tall one. -> the approved artboard.
   */
  readonly variant = input<FoamVariant>('cap');

  private readonly geometry = computed(() => GEOMETRY[this.variant()]);

  readonly height = computed(() => {
    const g = this.geometry();
    return g.band + g.radius + RIM;
  });

  readonly path = computed(() => {
    const { count, radius, band, up } = this.geometry();
    const chord = radius * 2;
    const strip = this.height();

    // Sweep 1 travelling right bulges up; sweep 1 travelling left bulges down.
    // The run is written once here rather than per section — the last time it
    // was repeated by hand the copies drifted apart.
    //
    // Both forms leave RIM of clear strip past the bubbles' far edge: a cap's
    // crests stop at y=RIM rather than at 0, a hood's at strip-RIM rather than
    // at the bottom. That gap is what the crescent is drawn into.
    if (up) {
      const arcs = ` a${radius},${radius} 0 0 1 ${chord},0`.repeat(count);
      return `M0,${strip} L0,${RIM + radius}${arcs} L1440,${strip} Z`;
    }
    const arcs = ` a${radius},${radius} 0 0 1 -${chord},0`.repeat(count);
    return `M0,0 L1440,0 L1440,${band}${arcs} Z`;
  });

  /**
   * The same run again, pushed a fraction of the radius to the OUTSIDE of the
   * bubbles — down under a hood, up over a cap — so it shows only as a crescent
   * along each bubble's outer edge.
   *
   * This is what gives the seam a value of its own. The front path has to stay
   * exactly the colour of the section it leads into or a hard band appears
   * there, so nothing about the FILL could ever have made this edge visible;
   * the crescent is a second shape behind it, which is also what real suds look
   * like. One extra <path> and one token, rather than a stroke — an outline
   * would turn soft foam into a graphic.
   *
   * It is offset by exactly RIM, which is exactly the clear strip the path
   * leaves on that side, so the crescent lands in that gap and is never cropped.
   */
  readonly rimOffset = computed(() => (this.geometry().up ? -RIM : RIM));
}
