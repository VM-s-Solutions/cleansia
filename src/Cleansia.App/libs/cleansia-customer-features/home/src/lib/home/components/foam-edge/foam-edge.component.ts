import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Which way the scallops face, and how tall the strip is. */
export type FoamVariant = 'cap' | 'cap-short' | 'hood';

/**
 * The scalloped divider between sections — the suds motif the mobile apps
 * already use for pull-to-refresh (`SudsRefreshIndicator`), carried onto the web
 * as structure rather than decoration.
 *
 * Two shapes, not one shape flipped. A `cap` fills the BOTTOM of its strip and
 * arcs bumps upward into the section above; a `hood` fills the TOP and hangs
 * bumps downward into the section below. `scaleY(-1)` on a cap does not produce
 * a hood — it produces a cap pointing the wrong way, with the fill on the wrong
 * side of the line, which is how the divider under the gallery came out as a
 * band of blue rectangles.
 */
@Component({
  selector: 'cleansia-foam-edge',
  templateUrl: './foam-edge.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FoamEdgeComponent {
  /** Colour the scallops are cut out of — the section the edge leads into. */
  readonly fill = input<string>('var(--cl-foam, var(--surface-card))');

  /**
   * `cap` is the hero's 90-tall edge, `cap-short` the gallery's 60-tall one,
   * `hood` the 60-tall edge that opens a tinted band. -> the approved artboard.
   */
  readonly variant = input<FoamVariant>('cap');

  readonly height = computed(() => (this.variant() === 'cap' ? 90 : 60));

  readonly path = computed(() => {
    // Twelve 60-radius half-circles across the 1440 viewBox. Written once here
    // rather than per section: the arc run drifted apart the last time it was
    // repeated by hand.
    const right = ' a60,60 0 0 1 120,0'.repeat(12);
    const left = ' a60,60 0 0 0 -120,0'.repeat(12);

    switch (this.variant()) {
      case 'cap':
        return `M0,90 L0,60${right} L1440,90 Z`;
      case 'cap-short':
        return `M0,60 L0,28${right} L1440,60 Z`;
      case 'hood':
        return `M0,0 L1440,0 L1440,32${left} Z`;
    }
  });
}
