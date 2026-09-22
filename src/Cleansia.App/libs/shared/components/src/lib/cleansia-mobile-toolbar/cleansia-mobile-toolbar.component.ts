import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { CleansiaBrandNameComponent } from '../cleansia-brand-name';
import { CleansiaButtonComponent } from '../cleansia-button';
import { CleansiaLanguageSwitcherComponent } from '../cleansia-language-switcher';

/**
 * The bar a signed-in phone reader sees instead of the sidebar rail: the menu button that opens
 * the drawer, the brand mark, and the language switcher. An app's own controls (the admin bell)
 * are projected in front of the switcher.
 *
 * @example
 * <cleansia-mobile-toolbar (menuOpen)="openSidebar()">
 *   <span class="cleansia-mobile-toolbar__action">…</span>
 * </cleansia-mobile-toolbar>
 */
@Component({
  selector: 'cleansia-mobile-toolbar',
  standalone: true,
  imports: [TranslatePipe, CleansiaBrandNameComponent, CleansiaButtonComponent, CleansiaLanguageSwitcherComponent],
  templateUrl: './cleansia-mobile-toolbar.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaMobileToolbarComponent {
  menuOpen = output<void>();
}
