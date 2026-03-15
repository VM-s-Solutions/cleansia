import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import {
  CleansiaCookieConsentComponent,
  CleansiaCustomerFooterComponent,
  CleansiaCustomerNavbarComponent,
} from '@cleansia/components';
import { PageTitleService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { filter, map, Subject, takeUntil } from 'rxjs';

@Component({
  imports: [
    RouterModule,
    TranslateModule,
    ToastModule,
    ConfirmDialogModule,
    CleansiaCustomerNavbarComponent,
    CleansiaCustomerFooterComponent,
    CleansiaCookieConsentComponent,
  ],
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly pageTitleService = inject(PageTitleService);
  private readonly destroy$ = new Subject<void>();

  isOnLandingPage = signal(true);

  constructor() {
    this.translate.addLangs(['cs', 'en', 'sk', 'uk', 'ru']);
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
      });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private detectLanguage(): string {
    const supported = ['cs', 'en', 'sk', 'uk', 'ru'];
    const stored = localStorage.getItem('preferred_language');
    if (stored && supported.includes(stored)) return stored;
    const browserLang = navigator.language?.split('-')[0]?.toLowerCase();
    if (browserLang && supported.includes(browserLang)) return browserLang;
    return 'en';
  }
}
