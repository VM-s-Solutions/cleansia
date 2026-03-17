import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { filter, map, take } from 'rxjs';

export interface PageTitleConfig {
  /** The base title for the application (e.g., "Cleansia Admin" or "Cleansia Partner") */
  baseTitle: string;
  /** Optional default title key when no route title is defined */
  defaultTitleKey?: string;
  /** Optional favicon path */
  faviconPath?: string;
}

@Injectable({
  providedIn: 'root',
})
export class PageTitleService {
  private readonly titleService = inject(Title);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);

  private baseTitle = 'Cleansia';
  private faviconLink: HTMLLinkElement | null = null;

  /**
   * Initialize the page title service with configuration
   * Call this method once in your app component's ngOnInit
   */
  initialize(config: PageTitleConfig): void {
    this.baseTitle = config.baseTitle;

    // Set up favicon if provided
    if (config.faviconPath) {
      this.setupFavicon(config.faviconPath);
    }

    // Listen for route changes
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        map(() => this.getRouteTitle(this.router.routerState.snapshot.root))
      )
      .subscribe((titleKey) => {
        this.updateTitle(titleKey || config.defaultTitleKey);
      });

    // Also update when language changes
    this.translate.onLangChange.subscribe(() => {
      const titleKey = this.getRouteTitle(this.router.routerState.snapshot.root);
      this.updateTitle(titleKey || config.defaultTitleKey);
    });

    // Set initial title
    const initialTitleKey = this.getRouteTitle(this.router.routerState.snapshot.root);
    this.updateTitle(initialTitleKey || config.defaultTitleKey);
  }

  /**
   * Manually set the page title with a translation key
   */
  setTitle(titleKey: string): void {
    this.updateTitle(titleKey);
  }

  /**
   * Manually set the page title with a raw string (not translated)
   */
  setTitleRaw(title: string): void {
    const fullTitle = title ? `${title} | ${this.baseTitle}` : this.baseTitle;
    this.titleService.setTitle(fullTitle);
  }

  /**
   * Update the favicon dynamically
   */
  setFavicon(path: string): void {
    this.setupFavicon(path);
  }

  private updateTitle(titleKey?: string): void {
    if (titleKey) {
      // Use stream to handle the case where translations haven't loaded yet.
      // instant() returns the key itself if translations aren't ready.
      this.translate.stream(titleKey).pipe(take(1)).subscribe((translatedTitle) => {
        const fullTitle = `${translatedTitle} | ${this.baseTitle}`;
        this.titleService.setTitle(fullTitle);
      });
    } else {
      this.titleService.setTitle(this.baseTitle);
    }
  }

  private getRouteTitle(route: ActivatedRouteSnapshot): string | undefined {
    // Traverse the route tree to find the deepest route with a title
    let title: string | undefined;
    let currentRoute: ActivatedRouteSnapshot | null = route;

    while (currentRoute) {
      if (currentRoute.data && currentRoute.data['title']) {
        title = currentRoute.data['title'];
      }
      currentRoute = currentRoute.firstChild;
    }

    return title;
  }

  private setupFavicon(path: string): void {
    // Find existing favicon link or create a new one
    this.faviconLink = this.document.querySelector('link[rel="icon"]') as HTMLLinkElement;

    if (!this.faviconLink) {
      this.faviconLink = this.document.createElement('link');
      this.faviconLink.rel = 'icon';
      this.faviconLink.type = 'image/x-icon';
      this.document.head.appendChild(this.faviconLink);
    }

    this.faviconLink.href = path;
  }
}
