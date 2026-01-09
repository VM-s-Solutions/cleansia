import { NgClass } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { loadAdminCodes } from '@cleansia/admin-stores';
import {
  CleansiaSidebarMenuComponent,
  SidebarMenuItem,
} from '@cleansia/components';
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
  ],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly authService = inject(AdminAuthService);

  sidebarCollapsed = signal(false);

  constructor() {
    this.translate.addLangs(['cs', 'en']);
    this.translate.setDefaultLang('cs');
    this.translate.use('cs');
  }

  ngOnInit(): void {
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
    {
      label: this.translate.instant('sidebar.employees'),
      icon: 'pi pi-users',
      route: '/employee-management',
    },
    {
      label: this.translate.instant('sidebar.payPeriods'),
      icon: 'pi pi-calendar',
      route: '/pay-periods',
    },
    {
      label: this.translate.instant('sidebar.orders'),
      icon: 'pi pi-shopping-cart',
      route: '/order-management',
    },
    {
      label: this.translate.instant('sidebar.invoices'),
      icon: 'pi pi-file',
      route: '/invoice-management',
    },
    {
      label: this.translate.instant('sidebar.reports'),
      icon: 'pi pi-chart-bar',
      route: '/reports',
    },
    {
      label: this.translate.instant('sidebar.services'),
      icon: 'pi pi-wrench',
      route: '/service-management',
    },
    {
      label: this.translate.instant('sidebar.packages'),
      icon: 'pi pi-box',
      route: '/package-management',
    },
    {
      label: this.translate.instant('sidebar.adminUsers'),
      icon: 'pi pi-user-plus',
      route: '/admin-user-management',
    },
    {
      label: this.translate.instant('sidebar.languages'),
      icon: 'pi pi-globe',
      route: '/language-management',
    },
    {
      label: this.translate.instant('sidebar.countries'),
      icon: 'pi pi-map',
      route: '/country-management',
    },
    {
      label: this.translate.instant('sidebar.currencies'),
      icon: 'pi pi-dollar',
      route: '/currency-management',
    },
    {
      label: this.translate.instant('sidebar.companyInfo'),
      icon: 'pi pi-building',
      route: '/company-info',
    },
    {
      label: this.translate.instant('sidebar.templates'),
      icon: 'pi pi-file-edit',
      route: '/template-management',
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
