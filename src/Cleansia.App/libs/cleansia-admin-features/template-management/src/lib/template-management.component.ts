import { Component, inject, signal, OnInit } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TabsModule } from 'primeng/tabs';
import { filter } from 'rxjs';
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
  styleUrl: './template-management.component.scss',
})
export class TemplateManagementComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  tabs: TemplateTab[] = [
    {
      label: this.translate.instant('pages.template_management.tabs.invoice'),
      route: 'invoice-templates',
      icon: 'pi pi-file',
    },
    {
      label: this.translate.instant('pages.template_management.tabs.receipt'),
      route: 'receipt-templates',
      icon: 'pi pi-receipt',
    },
    {
      label: this.translate.instant('pages.template_management.tabs.email'),
      route: 'email-templates',
      icon: 'pi pi-envelope',
    },
  ];

  activeTabIndex = signal(0);

  ngOnInit(): void {
    this.updateActiveTab();

    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.updateActiveTab();
      });
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
      this.router.navigate(['/template-management', tab.route]);
    }
  }
}
