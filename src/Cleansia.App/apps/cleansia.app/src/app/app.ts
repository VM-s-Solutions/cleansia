import { NgClass } from '@angular/common';
import { Component, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import {
  CleansiaCookieConsentComponent,
  CleansiaLanguageSwitcherComponent,
} from '@cleansia/components';
import { CustomerAuthService } from '@cleansia/customer-services';
import {
  CleansiaCustomerRoute,
  DialogService,
  PageTitleService,
} from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MenubarModule } from 'primeng/menubar';
import { ToastModule } from 'primeng/toast';
import { filter, map, Subject, takeUntil } from 'rxjs';

@Component({
  imports: [
    NgClass,
    RouterModule,
    TranslateModule,
    ToastModule,
    ConfirmDialogModule,
    MenubarModule,
    ButtonModule,
    CleansiaLanguageSwitcherComponent,
    CleansiaCookieConsentComponent,
  ],
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly pageTitleService = inject(PageTitleService);
  private readonly dialogService = inject(DialogService);
  private readonly destroy$ = new Subject<void>();

  isMobile = signal(false);
  mobileMenuOpen = signal(false);
  isOnLandingPage = signal(true);
  currentYear = signal(new Date().getFullYear());

  constructor() {
    this.updateMobileStatus();
    this.translate.addLangs(['cs', 'en', 'pl']);
    this.translate.setDefaultLang('en');
    this.translate.use(this.detectLanguage());
  }

  ngOnInit() {
    this.pageTitleService.initialize({
      baseTitle: 'Cleansia',
      defaultTitleKey: 'page_titles.customer.default',
      faviconPath: 'assets/logos/Logo.ico',
    });

    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        map((event) => (event as NavigationEnd).url),
        takeUntil(this.destroy$)
      )
      .subscribe((url) => {
        this.isOnLandingPage.set(url === '/' || url === '');
        this.mobileMenuOpen.set(false);
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

  toggleMobileMenu(): void {
    this.mobileMenuOpen.set(!this.mobileMenuOpen());
  }

  navigateTo(route: string): void {
    this.router.navigate([route]);
    this.mobileMenuOpen.set(false);
  }

  logout(): void {
    this.dialogService
      .confirmTranslated(
        'global.dialog.confirm_logout',
        'global.dialog.confirm'
      )
      .subscribe((confirmed) => {
        if (confirmed) {
          this.authService.logout();
        }
      });
  }

  private detectLanguage(): string {
    const supported = ['cs', 'en', 'pl'];
    const stored = localStorage.getItem('preferred_language');
    if (stored && supported.includes(stored)) {
      return stored;
    }
    const browserLang = navigator.language?.split('-')[0]?.toLowerCase();
    if (browserLang && supported.includes(browserLang)) {
      return browserLang;
    }
    return 'en';
  }

  private updateMobileStatus() {
    this.isMobile.set(window.innerWidth < 768);
  }
}
