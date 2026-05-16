import { Component, inject, OnDestroy, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import {
  CleansiaCookieConsentComponent,
  CleansiaDevBannerComponent,
} from '@cleansia/components';
import { CleansiaCustomerFooterComponent } from './components/footer/customer-footer.component';
import { CleansiaCustomerNavbarComponent } from './components/navbar/customer-navbar.component';
import { ConsentSyncService } from '@cleansia/customer-services';
import { environment } from '../environments/environment';
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
    CleansiaDevBannerComponent,
  ],
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly pageTitleService = inject(PageTitleService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly consentSync = inject(ConsentSyncService);
  private readonly destroy$ = new Subject<void>();

  readonly consentSyncFn = this.consentSync.getSyncFn();
  readonly bugReportUrl = environment.bugReportUrl;
  isOnLandingPage = signal(true);

  constructor() {
    // Translation initialization is handled by APP_INITIALIZER in app.config.ts
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

}
