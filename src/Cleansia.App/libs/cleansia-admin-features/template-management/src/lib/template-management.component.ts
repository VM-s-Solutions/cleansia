import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TabsModule } from 'primeng/tabs';
import { filter, Subject, takeUntil } from 'rxjs';
import {
  CleansiaLanguageSwitcherComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';

interface TemplateTab {
  label: string;
  route: string;
  icon: string;
}

@Component({
  selector: 'lib-template-management',
  standalone: true,
  imports: [
    RouterOutlet,
    TranslateModule,
    TabsModule,
    CleansiaLanguageSwitcherComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './template-management.component.html',
})
export class TemplateManagementComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private destroy$ = new Subject<void>();

  tabs: TemplateTab[] = [];

  activeTabIndex = signal(0);

  ngOnInit(): void {
    this.rebuildTabs();
    this.updateActiveTab();

    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.updateActiveTab();
      });

    // Rebuild tabs when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTabs();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTabs(): void {
    this.tabs = [
      {
        label: this.translate.instant('pages.template_management.tabs.email'),
        route: 'email-templates',
        icon: 'pi pi-envelope',
      },
    ];
  }

  private updateActiveTab(): void {
    const currentUrl = this.router.url;
    const index = this.tabs.findIndex((tab) => currentUrl.includes(tab.route));
    if (index >= 0) {
      this.activeTabIndex.set(index);
    }
  }

  onTabChange(index: number | string): void {
    const numIndex = typeof index === 'string' ? parseInt(index, 10) : index;
    this.activeTabIndex.set(numIndex);
    const tab = this.tabs[numIndex];
    if (tab) {
      this.router.navigate([CleansiaAdminRoute.TEMPLATE_MANAGEMENT, tab.route]);
    }
  }
}
