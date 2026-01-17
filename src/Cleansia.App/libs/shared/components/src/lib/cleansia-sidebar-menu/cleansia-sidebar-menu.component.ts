import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  HostListener,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { RippleModule } from 'primeng/ripple';
import { TooltipModule } from 'primeng/tooltip';
import { CleansiaBrandNameComponent } from '../cleansia-brand-name';
import { CleansiaButtonComponent } from '../cleansia-button';
import { SidebarMenuItem } from './cleansia-sidebar-menu.models';
import { filter } from 'rxjs';

@Component({
  selector: 'cleansia-sidebar-menu',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    TranslateModule,
    RippleModule,
    TooltipModule,
    CleansiaBrandNameComponent,
    CleansiaButtonComponent,
  ],
  templateUrl: './cleansia-sidebar-menu.component.html',
  styleUrls: ['../../../../assets/src/styles/components/cleansia-sidebar-menu.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaSidebarMenuComponent {
  private readonly router = inject(Router);
  private readonly el = inject(ElementRef);

  // Inputs
  menuItems = input<SidebarMenuItem[]>([]);
  isRoot = input(true);
  collapsed = input(false);

  // Outputs
  collapsedChange = output<boolean>();
  menuItemSelected = output<string>();

  // Signals
  private isMobileSignal = signal(false);
  isSidebarExpanded = signal(false);
  private localCollapsed = signal(false);
  currentRoute = signal<string>('');

  // Computed
  effectiveCollapsed = computed(() =>
    this.isRoot() ? this.localCollapsed() : this.collapsed()
  );

  isMobile = computed(() => this.isMobileSignal());

  constructor() {
    this.updateMobileStatus();
    this.currentRoute.set(this.router.url);

    // Subscribe to route changes
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.currentRoute.set(this.router.url);
      });
  }

  @HostListener('window:resize')
  onResize() {
    this.updateMobileStatus();
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

  toggleSidebar(): void {
    this.isSidebarExpanded.update((v) => !v);
  }

  onMenuItemClick(item: SidebarMenuItem, event: Event): void {
    event.stopPropagation();

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

    // Toggle submenu
    if (item.children?.length) {
      item.expanded = !(item.expanded ?? false);
    }
  }

  isActiveRoute(itemRoute: string | undefined): boolean {
    if (!itemRoute) return false;
    const currentUrl = this.currentRoute();
    return currentUrl === itemRoute || currentUrl.startsWith(itemRoute + '/');
  }

  getTooltipText(item: SidebarMenuItem): string {
    return this.effectiveCollapsed() ? item.label : '';
  }
}
