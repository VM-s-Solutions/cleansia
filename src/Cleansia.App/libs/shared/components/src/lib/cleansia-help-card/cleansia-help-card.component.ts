import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  HostBinding,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

export interface HelpStep {
  icon: string;
  titleKey: string;
  descriptionKey: string;
}

export interface StatusFlowItem {
  statusKey: string;
  descriptionKey: string;
  colorClass: string;
}

/**
 * Cleansia Help Card Component
 *
 * A collapsible help card that displays workflow steps with icons.
 * Can be dismissed and remembers dismissal state via localStorage.
 * When dismissed, emits an event so parent can show restore button elsewhere.
 *
 * @example
 * <cleansia-help-card
 *   [titleKey]="'help.orders.title'"
 *   [steps]="orderSteps"
 *   [storageKey]="'orders-help-dismissed'"
 *   [statusFlow]="orderStatusFlow"
 *   [showRestoreButton]="false"
 *   (dismissedChange)="onHelpDismissed($event)"
 * />
 */
@Component({
  selector: 'cleansia-help-card',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './cleansia-help-card.component.html',
  styles: [':host.hidden { display: none; }'],
})
export class CleansiaHelpCardComponent implements OnInit {
  /** Translation key for the card title */
  titleKey = input<string>('');

  /** Array of help steps to display */
  steps = input<HelpStep[]>([]);

  /** localStorage key for remembering dismissal */
  storageKey = input<string>('');

  /** Whether the card is collapsible */
  collapsible = input<boolean>(true);

  /** Initial collapsed state */
  initiallyCollapsed = input<boolean>(false);

  /** Whether to show the dismiss button */
  showDismiss = input<boolean>(true);

  /** Whether to show the restore button when dismissed (set to false if parent handles it) */
  showRestoreButton = input<boolean>(true);

  /** Optional status flow items to explain statuses */
  statusFlow = input<StatusFlowItem[]>([]);

  /** Translation key for status flow section title */
  statusFlowTitleKey = input<string>('');

  /** Emits when dismissed state changes */
  dismissedChange = output<boolean>();

  /** Internal state */
  isCollapsed = signal<boolean>(false);
  isDismissed = signal<boolean>(false);

  /** Whether we have status flow items to display */
  hasStatusFlow = computed(() => this.statusFlow().length > 0);

  /** Whether the component should be hidden (dismissed with no restore button) */
  @HostBinding('class.hidden')
  get isHidden(): boolean {
    return this.isDismissed() && !this.showRestoreButton();
  }

  ngOnInit(): void {
    // Check localStorage for dismissal state
    if (this.storageKey()) {
      const dismissed = localStorage.getItem(this.storageKey());
      if (dismissed === 'true') {
        this.isDismissed.set(true);
      }
    }
    if (this.initiallyCollapsed()) {
      this.isCollapsed.set(true);
    }
  }

  toggleCollapse(): void {
    if (this.collapsible()) {
      this.isCollapsed.update((v) => !v);
    }
  }

  dismiss(): void {
    this.isDismissed.set(true);
    if (this.storageKey()) {
      localStorage.setItem(this.storageKey(), 'true');
    }
    this.dismissedChange.emit(true);
  }

  restore(): void {
    this.isDismissed.set(false);
    this.isCollapsed.set(false);
    if (this.storageKey()) {
      localStorage.removeItem(this.storageKey());
    }
    this.dismissedChange.emit(false);
  }

  /** Check if help is dismissed (for external use) */
  static isHelpDismissed(storageKey: string): boolean {
    return localStorage.getItem(storageKey) === 'true';
  }

  /** Restore help from external call */
  static restoreHelp(storageKey: string): void {
    localStorage.removeItem(storageKey);
  }
}
