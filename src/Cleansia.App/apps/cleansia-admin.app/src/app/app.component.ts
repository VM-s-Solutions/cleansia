import { NgClass } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { loadAdminCodes } from '@cleansia/admin-stores';
import {
  CleansiaCookieConsentComponent,
  CleansiaDevBannerComponent,
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

  readonly bugReportUrl = environment.bugReportUrl;
  sidebarCollapsed = signal(false);

  ngOnInit(): void {
    // Initialize page title service
    this.pageTitleService.initialize({
      baseTitle: 'Cleansia Admin',
      defaultTitleKey: 'page_titles.admin.default',
      faviconPath: 'assets/logos/Logo.ico',
    });

    // Load codes on app initialization
    this.store.dispatch(loadAdminCodes());
  }

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

  onSidebarCollapsedChange(collapsed: boolean): void {
    this.sidebarCollapsed.set(collapsed);
  }

  sidebarMenuItems: SidebarMenuItem[] = [
    { label: 'sidebar.employees', icon: 'pi pi-users', route: '/employee-management' },
    { label: 'sidebar.payPeriods', icon: 'pi pi-calendar', route: '/pay-periods' },
    { label: 'sidebar.orders', icon: 'pi pi-shopping-cart', route: '/order-management' },
    { label: 'sidebar.invoices', icon: 'pi pi-file', route: '/invoice-management' },
    { label: 'sidebar.reports', icon: 'pi pi-chart-bar', route: '/reports' },
    { label: 'sidebar.services', icon: 'pi pi-wrench', route: '/service-management' },
    { label: 'sidebar.packages', icon: 'pi pi-box', route: '/package-management' },
    { label: 'sidebar.payConfigs', icon: 'pi pi-money-bill', route: '/pay-config-management' },
    { label: 'sidebar.adminUsers', icon: 'pi pi-user-plus', route: '/admin-user-management' },
    { label: 'sidebar.languages', icon: 'pi pi-globe', route: '/language-management' },
    { label: 'sidebar.countries', icon: 'pi pi-map', route: '/country-management' },
    { label: 'sidebar.currencies', icon: 'pi pi-dollar', route: '/currency-management' },
    { label: 'sidebar.companyInfo', icon: 'pi pi-building', route: '/company-info' },
    { label: 'sidebar.templates', icon: 'pi pi-file-edit', route: '/template-management' },
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
