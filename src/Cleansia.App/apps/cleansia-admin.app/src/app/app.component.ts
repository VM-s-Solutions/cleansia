import { isPlatformBrowser, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { AdminAuthService, AdminNotificationBadgeService } from '@cleansia/admin-services';
import { loadAdminCodes } from '@cleansia/admin-stores';
import {
  CleansiaButtonComponent,
  CleansiaCookieConsentComponent,
  CleansiaDevBannerComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaSidebarMenuComponent,
  isMobileViewport,
  SidebarMenuItem,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { DialogService, PageTitleService, Policy } from '@cleansia/services';
import { environment } from '../environments/environment';
import { ADMIN_MENU_ITEMS, NOTIFICATIONS_ROUTE } from './admin-menu';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';

@Component({
  imports: [
    NgClass,
    ToastModule,
    ConfirmDialogModule,
    RouterModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaSidebarMenuComponent,
    CleansiaCookieConsentComponent,
    CleansiaDevBannerComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaPermissionDirective,
  ],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly authService = inject(AdminAuthService);
  private readonly pageTitleService = inject(PageTitleService);
  private readonly dialogService = inject(DialogService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  protected readonly notificationBadge = inject(AdminNotificationBadgeService);
  protected readonly Policy = Policy;
  protected readonly notificationsRoute = NOTIFICATIONS_ROUTE;

  readonly bugReportUrl = environment.bugReportUrl;
  sidebarCollapsed = signal(false);
  mobileSidebarExpanded = signal(false);
  private mobileSignal = signal(false);

  ngOnInit(): void {
    this.pageTitleService.initialize({
      baseTitle: 'Cleansia Admin',
      defaultTitleKey: 'page_titles.admin.default',
    });

    this.store.dispatch(loadAdminCodes());

    if (this.isBrowser) {
      this.updateMobileStatus();
      window.addEventListener('resize', () => this.updateMobileStatus());
      this.notificationBadge.start();
    }
  }

  readonly isLoggedIn = toSignal(this.authService.isLoggedIn$, { initialValue: false });

  isMobile(): boolean {
    return this.mobileSignal();
  }

  openSidebar(): void {
    this.mobileSidebarExpanded.set(true);
  }

  onSidebarCollapsedChange(collapsed: boolean): void {
    this.sidebarCollapsed.set(collapsed);
  }

  private updateMobileStatus(): void {
    this.mobileSignal.set(isMobileViewport(window.innerWidth));
  }

  // Only the notifications entry is re-created when the badge moves; every other item keeps its
  // identity, and with it the expanded state the sidebar writes onto the object.
  readonly sidebarMenuItems = computed<SidebarMenuItem[]>(() => {
    const badge = this.notificationBadge.badgeLabel();
    return this.menuItems.map((item) =>
      item.route === NOTIFICATIONS_ROUTE ? { ...item, badge: badge ?? undefined } : item
    );
  });

  private readonly menuItems: SidebarMenuItem[] = [
    ...ADMIN_MENU_ITEMS,
    {
      label: 'sidebar.logout',
      icon: 'pi pi-sign-out',
      permission: Policy.Authenticated,
      onClickFn: () => {
        this.dialogService
          .confirmTranslated('global.dialog.confirm_logout', 'global.dialog.confirm')
          .subscribe((confirmed) => {
            if (confirmed) this.authService.logout().subscribe();
          });
      },
    },
  ];
}
