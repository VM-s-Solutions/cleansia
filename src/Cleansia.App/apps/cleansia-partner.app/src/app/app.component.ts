import { NgClass } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import {
  CleansiaRegistrationLockComponent,
  CleansiaSidebarMenuComponent,
  SidebarMenuItem,
} from '@cleansia/components';
import {
  AuthService,
  CleansiaPartnerRoute,
  RegistrationCompletionService,
} from '@cleansia/services';
import { checkEmployeeCurrent } from '@cleansia/stores';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
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
    RouterModule,
    CleansiaSidebarMenuComponent,
    CleansiaRegistrationLockComponent,
  ],
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly translate = inject(TranslateService);
  private readonly registrationService = inject(RegistrationCompletionService);

  private readonly destroy$ = new Subject<void>();
  private hasCheckedEmployee = false;

  sidebarCollapsed = signal(false);
  shouldShowRegistrationLock = signal(false);

  constructor() {
    this.translate.addLangs(['cs', 'en']);
    this.translate.setDefaultLang('cs');
    this.translate.use('cs');
  }

  ngOnInit() {
    const currentUrl$ = this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => (event as NavigationEnd).url),
      startWith(this.router.url)
    );

    const isLoggedIn$ = this.authService.isLoggedIn$.asObservable();
    const registrationStatus$ =
      this.registrationService.isRegistrationComplete();

    combineLatest([currentUrl$, isLoggedIn$, registrationStatus$])
      .pipe(distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(([url, isLoggedIn, isComplete]) => {
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

  onSidebarCollapsedChange(collapsed: boolean): void {
    this.sidebarCollapsed.set(collapsed);
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
        this.authService.logout();
      },
    },
  ];
}
