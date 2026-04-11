import { isPlatformBrowser, NgClass } from '@angular/common';
import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { loadAdminCodes } from '@cleansia/admin-stores';
import {
  CleansiaCookieConsentComponent,
  CleansiaDevBannerComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaSidebarMenuComponent,
  SidebarMenuItem,
} from '@cleansia/components';
import { DialogService, PageTitleService } from '@cleansia/services';
import { environment } from '../environments/environment';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';

@Component({
  imports: [
    NgClass,
    ToastModule,
    ConfirmDialogModule,
    RouterModule,
    CleansiaSidebarMenuComponent,
    CleansiaCookieConsentComponent,
    CleansiaDevBannerComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly authService = inject(AdminAuthService);
  private readonly pageTitleService = inject(PageTitleService);
  private readonly dialogService = inject(DialogService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly bugReportUrl = environment.bugReportUrl;
  sidebarCollapsed = signal(false);
  mobileSidebarExpanded = signal(false);
  private mobileSignal = signal(false);

  ngOnInit(): void {
    this.pageTitleService.initialize({
      baseTitle: 'Cleansia Admin',
      defaultTitleKey: 'page_titles.admin.default',
      faviconPath: 'assets/logos/Logo.ico',
    });

    this.store.dispatch(loadAdminCodes());

    if (this.isBrowser) {
      this.updateMobileStatus();
      window.addEventListener('resize', () => this.updateMobileStatus());
    }
  }

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

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
    this.mobileSignal.set(window.innerWidth < 768);
  }

  sidebarMenuItems: SidebarMenuItem[] = [
    { label: 'sidebar.employees', icon: 'pi pi-users', route: '/employee-management' },
    { label: 'sidebar.pay_periods', icon: 'pi pi-calendar', route: '/pay-periods' },
    { label: 'sidebar.orders', icon: 'pi pi-shopping-cart', route: '/order-management' },
    { label: 'sidebar.invoices', icon: 'pi pi-file', route: '/invoice-management' },
    { label: 'sidebar.reports', icon: 'pi pi-chart-bar', route: '/reports' },
    { label: 'sidebar.services', icon: 'pi pi-wrench', route: '/service-management' },
    { label: 'sidebar.packages', icon: 'pi pi-box', route: '/package-management' },
    { label: 'sidebar.global_rates', icon: 'pi pi-money-bill', route: '/pay-config-management' },
    { label: 'sidebar.admin_users', icon: 'pi pi-user-plus', route: '/admin-user-management' },
    { label: 'sidebar.languages', icon: 'pi pi-globe', route: '/language-management' },
    { label: 'sidebar.countries', icon: 'pi pi-map', route: '/country-management' },
    { label: 'sidebar.currencies', icon: 'pi pi-dollar', route: '/currency-management' },
    { label: 'sidebar.company_info', icon: 'pi pi-building', route: '/company-info' },
    { label: 'sidebar.templates', icon: 'pi pi-file-edit', route: '/template-management' },
    { label: 'sidebar.fiscal_failures', icon: 'pi pi-exclamation-triangle', route: '/fiscal-failures' },
    {
      label: 'sidebar.logout',
      icon: 'pi pi-sign-out',
      onClickFn: () => {
        this.dialogService
          .confirmTranslated('global.dialog.confirm_logout', 'global.dialog.confirm')
          .subscribe((confirmed) => {
            if (confirmed) this.authService.logout();
          });
      },
    },
  ];
}
