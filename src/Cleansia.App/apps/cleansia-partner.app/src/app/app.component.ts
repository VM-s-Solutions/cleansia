import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import {
  CleansiaCookieConsentComponent,
  CleansiaDevBannerComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaSidebarMenuComponent,
  SidebarMenuItem,
} from '@cleansia/components';
import { CleansiaRegistrationLockComponent } from './components/registration-lock/registration-lock.component';
import {
  PartnerAuthService,
  RegistrationCompletionService,
} from '@cleansia/partner-services';
import { environment } from '../environments/environment';
import {
  checkEmployeeCurrent,
  loadCodes,
  selectEmployeeConfirmation,
} from '@cleansia/partner-stores';
import { CleansiaPartnerRoute, CommonRoute, DialogService, PageTitleService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import {
  combineLatest,
  distinctUntilChanged,
  filter,
  map,
  startWith,
  Subject,
  takeUntil,
} from 'rxjs';

@Component({
  imports: [
    NgClass,
    ToastModule,
    ConfirmDialogModule,
    RouterModule,
    CleansiaSidebarMenuComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaRegistrationLockComponent,
    CleansiaCookieConsentComponent,
    CleansiaDevBannerComponent,
  ],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(PartnerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly registrationService = inject(RegistrationCompletionService);
  private readonly pageTitleService = inject(PageTitleService);
  private readonly dialogService = inject(DialogService);

  private readonly destroy$ = new Subject<void>();
  private hasCheckedEmployee = false;

  readonly bugReportUrl = environment.bugReportUrl;
  sidebarCollapsed = signal(false);
  mobileSidebarExpanded = signal(false);
  shouldShowRegistrationLock = signal(false);
  isMobile = signal(false);

  constructor() {
    this.updateMobileStatus();
  }

  ngOnInit() {
    // Initialize page title service
    this.pageTitleService.initialize({
      baseTitle: 'Cleansia Partner',
      defaultTitleKey: 'page_titles.partner.default',
      faviconPath: 'assets/logos/Logo.ico',
    });

    // Load codes on app initialization
    this.store.dispatch(loadCodes());

    const currentUrl$ = this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => (event as NavigationEnd).url),
      startWith(this.router.url)
    );

    const isLoggedIn$ = this.authService.isLoggedIn$.asObservable();
    const employeeStatus$ = this.store.select(selectEmployeeConfirmation);

    combineLatest([currentUrl$, isLoggedIn$, employeeStatus$])
      .pipe(distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(([url, isLoggedIn, employeeStatus]) => {
        const isComplete = this.registrationService.isRegistrationComplete(
          employeeStatus || null
        );
        if (isLoggedIn && !this.hasCheckedEmployee) {
          this.store.dispatch(checkEmployeeCurrent());
          this.hasCheckedEmployee = true;
        } else if (!isLoggedIn) {
          this.hasCheckedEmployee = false;
        }

        const isProtectedRoute = this.shouldCheckRegistrationCompletion(url);
        // Only show lock when we have confirmed incomplete status (not while loading)
        const shouldShow = isLoggedIn && isProtectedRoute && employeeStatus != null && !isComplete;

        this.shouldShowRegistrationLock.set(shouldShow);
      });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  readonly isLoggedIn = toSignal(this.authService.isLoggedIn$, { initialValue: false });

  @HostListener('window:resize')
  onResize() {
    this.updateMobileStatus();
  }

  private updateMobileStatus() {
    this.isMobile.set(window.innerWidth < 768);
  }

  onSidebarCollapsedChange(collapsed: boolean): void {
    this.sidebarCollapsed.set(collapsed);
  }

  openSidebar(): void {
    this.mobileSidebarExpanded.set(true);
  }

  private shouldCheckRegistrationCompletion(url: string): boolean {
    const excludedRoutes = [
      `/${CleansiaPartnerRoute.PROFILE}`,
      `/${CleansiaPartnerRoute.GDPR}`,
      `/${CommonRoute.NOT_FOUND}`,
      `/${CleansiaPartnerRoute.LOGIN}`,
      `/${CleansiaPartnerRoute.REGISTER}`,
      `/${CleansiaPartnerRoute.CONFIRM_EMAIL}`,
      `/${CleansiaPartnerRoute.FORGOT_PASSWORD}`,
    ];
    return !excludedRoutes.some((route) => url.startsWith(route));
  }

  sidebarMenuItems: SidebarMenuItem[] = [
    {
      label: 'sidebar.dashboard',
      icon: 'pi pi-home',
      route: `/${CleansiaPartnerRoute.DASHBOARD}`,
    },
    {
      label: 'sidebar.profile',
      icon: 'pi pi-user',
      route: `/${CleansiaPartnerRoute.PROFILE}`,
    },
    {
      label: 'sidebar.orders',
      icon: 'pi pi-shopping-cart',
      route: `/${CleansiaPartnerRoute.ORDERS}`,
    },
    {
      label: 'sidebar.invoices',
      icon: 'pi pi-file',
      route: `/${CleansiaPartnerRoute.INVOICES}`,
    },
    {
      label: 'sidebar.my_pay',
      icon: 'pi pi-wallet',
      route: `/${CleansiaPartnerRoute.MY_PAY}`,
    },
    {
      label: 'sidebar.logout',
      icon: 'pi pi-sign-out',
      onClickFn: () => {
        this.dialogService
          .confirmTranslated('global.dialog.confirm_logout', 'global.dialog.confirm')
          .subscribe((confirmed) => {
            if (confirmed) {
              this.authService.logout().pipe(takeUntil(this.destroy$)).subscribe();
            }
          });
      },
    },
  ];
}
