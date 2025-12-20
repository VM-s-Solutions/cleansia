import {
  animate,
  state,
  style,
  transition,
  trigger,
} from '@angular/animations';
import { NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { RippleModule } from 'primeng/ripple';
import { StyleClassModule } from 'primeng/styleclass';
import { CleansiaBrandNameComponent } from '../cleansia-brand-name';
import { CleansiaButtonComponent } from '../cleansia-button';
import { SidebarMenuItem } from './cleansia-sidebar-menu.models';

@Component({
  selector: 'cleansia-sidebar-menu',
  standalone: true,
  imports: [
    NgClass,
    RouterModule,
    RippleModule,
    TranslateModule,
    StyleClassModule,
    CleansiaBrandNameComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './cleansia-sidebar-menu.component.html',
  animations: [
    trigger('sidebarWidth', [
      state(
        'expanded',
        style({
          width: '16rem',
          boxShadow: '2px 0 10px var(--cleansia-black-100)',
        })
      ),
      state(
        'collapsed',
        style({
          boxShadow: '1px 0 5px var(--cleansia-black-100)',
        })
      ),
      transition('expanded <=> collapsed', [animate('0.05s ease-in-out')]),
    ]),
    trigger('menuItemContent', [
      state(
        'expanded',
        style({
          gap: '1rem',
        })
      ),
      state(
        'collapsed',
        style({
          gap: '0',
        })
      ),
      transition('expanded <=> collapsed', [animate('0.05s ease-in-out')]),
    ]),
    trigger('menuItemLabel', [
      state(
        'expanded',
        style({
          opacity: 1,
        })
      ),
      state(
        'collapsed',
        style({
          width: '0',
          opacity: 0,
          flex: 0,
        })
      ),
      transition('expanded <=> collapsed', [animate('0.05s ease-in-out')]),
    ]),
    trigger('subMenu', [
      state(
        'void',
        style({
          height: '0',
          opacity: 0,
          overflow: 'hidden',
        })
      ),
      state(
        '*',
        style({
          height: '*',
          opacity: 1,
          overflow: 'visible',
        })
      ),
      transition('void <=> *', [animate('0.05s ease-in-out')]),
    ]),
    trigger('toggleIcon', [
      state(
        'expanded',
        style({
          transform: 'rotate(180deg)',
        })
      ),
      state(
        'collapsed',
        style({
          transform: 'rotate(0deg)',
        })
      ),
      transition('expanded <=> collapsed', [animate('0.05s ease-in-out')]),
    ]),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSidebarMenuComponent {
  private readonly router = inject(Router);
  private readonly el = inject(ElementRef);

  menuItems = input<SidebarMenuItem[]>([]);
  defaultLogoRoute = input<string>('');
  logoText = input<string>('Cleansia');
  isRoot = input(true);
  collapsed = input(false);

  collapsedChange = output<boolean>();

  menuItemSelected = output<string>();

  private isMobileSignal = signal(false);
  isSidebarExpanded = signal(false);
  private localCollapsed = signal(false);

  effectiveCollapsed = computed(() =>
    this.isRoot() ? this.localCollapsed() : this.collapsed()
  );

  constructor() {
    this.updateMobileStatus();
  }

  @HostListener('window:resize')
  onResize() {
    this.updateMobileStatus();
  }

  get isMobile() {
    return this.isMobileSignal.asReadonly();
  }

  private updateMobileStatus() {
    this.isMobileSignal.set(window.innerWidth < 768);
  }

  toggleCollapsed(): void {
    if (this.isRoot()) {
      const newValue = !this.localCollapsed();
      this.localCollapsed.set(newValue);
      this.collapsedChange.emit(newValue);
    }
  }

  onMenuItemClick(item: SidebarMenuItem): void {
    if (item.route) {
      this.router.navigate([item.route]);
      this.menuItemSelected.emit(item.route);
    }
    if (item.onClickFn) {
      item.onClickFn();
    }
    if (this.isRoot() && this.isMobile()) {
      this.isSidebarExpanded.set(false);
    }
    this.toggleSubMenu(item);
  }

  private toggleSubMenu(item: SidebarMenuItem): void {
    if (item.children?.length) {
      item.expanded = !(item.expanded ?? false);
    }
  }

  toggleSidebar(): void {
    this.isSidebarExpanded.update((v) => !v);
  }
}
