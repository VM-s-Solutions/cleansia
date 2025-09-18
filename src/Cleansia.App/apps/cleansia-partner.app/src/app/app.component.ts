import { Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  CleansiaSidebarMenuComponent,
  SidebarMenuItem,
} from '@cleansia/components';
import { AuthService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

@Component({
  imports: [RouterModule, CleansiaSidebarMenuComponent],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly authService = inject(AuthService);
  private readonly translate = inject(TranslateService);

  // Track sidebar collapsed state
  sidebarCollapsed = signal(false);

  constructor() {
    this.translate.addLangs(['cs', 'en']);
    this.translate.setDefaultLang('cs');
  }

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

  // Handle sidebar collapse state change
  onSidebarCollapsedChange(collapsed: boolean): void {
    this.sidebarCollapsed.set(collapsed);
  }

  sidebarMenuItems: SidebarMenuItem[] = [
    {
      label: this.translate.instant('sidebar.dashboard'),
      icon: 'pi pi-home',
      route: '/dashboard',
    },
    {
      label: this.translate.instant('sidebar.profile'),
      icon: 'pi pi-user',
      route: '/profile',
    },
    {
      label: this.translate.instant('sidebar.orders'),
      icon: 'pi pi-shopping-cart',
      route: '/orders',
    },
    {
      label: this.translate.instant('sidebar.logout'),
      icon: 'pi pi-sign-out',
      onClickFn: () => {
        this.authService.logout();
      },
    },
  ];
}
