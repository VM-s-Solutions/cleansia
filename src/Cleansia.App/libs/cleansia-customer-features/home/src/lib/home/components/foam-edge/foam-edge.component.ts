import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The scalloped divider between sections — the suds motif the mobile apps
 * already use for pull-to-refresh (`SudsRefreshIndicator`), carried onto the web
 * as structure rather than decoration.
 *
 * One component rather than inline SVG per section: the arc run is twelve
 * 60-radius half-circles across the 1440 viewBox, and repeating that by hand is
 * how the four hero bubbles and the wave divider drifted apart in the first
 * place. `flipped` points the scallops upward for a section that rises out of a
 * tinted band instead of falling into one.
 */
@Component({
  selector: 'cleansia-foam-edge',
  templateUrl: './foam-edge.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FoamEdgeComponent {
  /** Colour the scallops are cut out of — the section the edge leads into. */
  readonly fill = input<string>('var(--surface-card)');

  /** Scallops point up rather than down. */
  readonly flipped = input<boolean>(false);
}
