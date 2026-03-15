import { NgClass } from '@angular/common';
import { Component, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import {
  CleansiaCookieConsentComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaRegistrationLockComponent,
  CleansiaSidebarMenuComponent,
  SidebarMenuItem,
} from '@cleansia/components';
import {
  PartnerAuthService,
  RegistrationCompletionService,
} from '@cleansia/partner-services';
import {
  checkEmployeeCurrent,
  loadCodes,
  selectEmployeeConfirmation,
} from '@cleansia/partner-stores';
import { CleansiaPartnerRoute, DialogService, PageTitleService } from '@cleansia/services';
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
  ],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
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

  sidebarCollapsed = signal(false);
  mobileSidebarExpanded = signal(false);
  shouldShowRegistrationLock = signal(false);
  isMobile = signal(false);

  constructor() {
    this.updateMobileStatus();
    this.translate.addLangs(['cs', 'en', 'sk', 'uk', 'ru']);
    this.translate.setDefaultLang('en');
    this.translate.use(this.detectLanguage());
  }

  private detectLanguage(): string {
    const supported = ['cs', 'en', 'sk', 'uk', 'ru'];
    // 1. Check stored preference
    const stored = localStorage.getItem('preferred_language');
    if (stored && supported.includes(stored)) {
      return stored;
    }
    // 2. Detect from browser language
    const browserLang = navigator.language?.split('-')[0]?.toLowerCase();
    if (browserLang && supported.includes(browserLang)) {
      return browserLang;
    }
    // 3. Default to English
    return 'en';
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
        const shouldShow = isLoggedIn && isProtectedRoute && !isComplete;

        this.shouldShowRegistrationLock.set(shouldShow);
      });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

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
    return !url.startsWith(`/${CleansiaPartnerRoute.PROFILE}`);
  }

  sidebarMenuItems: SidebarMenuItem[] = [
    {
      label: this.translate.instant('sidebar.dashboard'),
      icon: 'pi pi-home',
      route: `/${CleansiaPartnerRoute.DASHBOARD}`,
    },
    {
      label: this.translate.instant('sidebar.profile'),
      icon: 'pi pi-user',
      route: `/${CleansiaPartnerRoute.PROFILE}`,
    },
    {
      label: this.translate.instant('sidebar.orders'),
      icon: 'pi pi-shopping-cart',
      route: `/${CleansiaPartnerRoute.ORDERS}`,
    },
    {
      label: this.translate.instant('sidebar.invoices'),
      icon: 'pi pi-file',
      route: `/${CleansiaPartnerRoute.INVOICES}`,
    },
    {
      label: this.translate.instant('sidebar.logout'),
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
