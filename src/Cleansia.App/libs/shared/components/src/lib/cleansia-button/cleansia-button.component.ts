import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ButtonModule, ButtonSeverity } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { InputSize } from '../cleansia-base-form/cleansia-base-form.models';

export type ButtonSize = 'small' | 'medium' | 'large';

/**
 * Cleansia Button Component
 *
 * A modern, feature-rich button component with enhanced visual design including:
 * - Glass morphism effects
 * - Smooth animations and transitions
 * - Enhanced shadows for depth
 * - Ripple effect on click
 * - Multiple size variants (small, medium, large)
 * - Icon-only button support with automatic circular shape
 * - Tooltip support
 * - Gradient overlays for different severities
 *
 * @example
 * // Basic button
 * <cleansia-button
 *   [label]="'Click Me'"
 *   (onClick)="handleClick()"
 * />
 *
 * @example
 * // Button with icon
 * <cleansia-button
 *   [label]="'Save'"
 *   [icon]="'pi pi-save'"
 *   [severity]="'success'"
 *   [buttonSize]="'large'"
 * />
 *
 * @example
 * // Icon-only button with tooltip
 * <cleansia-button
 *   [icon]="'pi pi-trash'"
 *   [severity]="'danger'"
 *   [buttonSize]="'small'"
 *   [tooltip]="'Delete item'"
 * />
 *
 * @example
 * // Outlined button
 * <cleansia-button
 *   [label]="'Cancel'"
 *   [outlined]="true"
 *   [severity]="'secondary'"
 * />
 */
@Component({
  selector: 'cleansia-button',
  standalone: true,
  imports: [CommonModule, ButtonModule, TranslateModule, TooltipModule],
  templateUrl: './cleansia-button.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaButtonComponent {
  buttonType = input<'button' | 'submit' | 'reset'>('button');
  style = input<'basic-button' | 'raised-button'>('raised-button');
  severity = input<ButtonSeverity>('primary');
  title = input<string>('');
  label = input<string>(''); // Alias for title to match PrimeNG API
  size = input<InputSize>('full-width');
  buttonSize = input<ButtonSize>('medium'); // Visual size (small, medium, large)
  icon = input<string | undefined>(undefined);
  iconPosition = input<'left' | 'right'>('left');
  iconOutlined = input<boolean>(false);
  rounded = input<boolean>(false);
  outlined = input<boolean>(false); // PrimeNG-compatible outlined property
  text = input<boolean>(false); // PrimeNG-compatible text property
  disabled = input<boolean>(false);
  loading = input<boolean>(false);
  className = input<string>('');
  tooltip = input<string>(''); // Tooltip text
  tooltipPosition = input<'top' | 'bottom' | 'left' | 'right'>('top');
  ariaLabel = input<string>('');

  onClick = output<MouseEvent>(); // PrimeNG-compatible output name
  clickFn = output<MouseEvent>(); // Legacy output name

  // Computed property to check if this is an icon-only button
  isIconOnly = computed(() => {
    const hasIcon = !!this.icon();
    const hasLabel = !!(this.label() || this.title());
    return hasIcon && !hasLabel;
  });

  // Icon-only buttons render no visible text, so they need a programmatic
  // accessible name. Text buttons already carry their label as the name.
  resolvedAriaLabel = computed(() =>
    this.isIconOnly() && this.ariaLabel() ? this.ariaLabel() : undefined,
  );

  handleClick(event: MouseEvent): void {
    this.clickFn.emit(event);
    this.onClick.emit(event);
  }
}
