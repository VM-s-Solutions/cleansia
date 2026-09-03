import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  HostListener,
  inject,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  Router,
  RouterModule,
} from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CustomerAuthService } from '@cleansia/customer-services';
import {
  loadCustomerUser,
  selectCustomerCurrentUser,
} from '@cleansia/customer-stores';
import { DialogService, ThemeService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { AvatarModule } from 'primeng/avatar';
import { ButtonModule } from 'primeng/button';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { Subject, takeUntil } from 'rxjs';

import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
} from '@cleansia/components';

/**
 * Width at which the full bar fits inside the floating pill.
 *
 * Measured, not guessed, and measured in EVERY locale: the brand, four links,
 * both controls, the sign-in link and the CTA need 1139px in English and 1331px
 * in Ukrainian, whose labels are the longest we ship. PrimeFlex's `md:` is 768,
 * so the bar was laid out from 768 up and its right-hand cluster ran as much as
 * 134px past the pill — and at 1200 it still overflowed by 105px in Ukrainian,
 * which is what "the layout breaks when I switch language" was.
 *
 * `agents/tools/check-nav-fits.mjs` re-measures all five locales against this
 * number, so a longer translation fails a check instead of shipping broken.
 * Keep in step with the media query in `cleansia-customer-navbar.component.scss`.
 */
const NAV_DESKTOP_MIN_WIDTH = 1360;

@Component({
  selector: 'cleansia-customer-navbar',
  standalone: true,
  imports: [
    RouterModule,
    FormsModule,
    TranslateModule,
    ButtonModule,
    AvatarModule,
    ToggleSwitchModule,
    CleansiaBrandNameComponent,
    CleansiaButtonComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './customer-navbar.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaCustomerNavbarComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly authService = inject(CustomerAuthService);
  private readonly dialogService = inject(DialogService);
  private readonly themeService = inject(ThemeService);
  private readonly store = inject(Store);
  private readonly elRef = inject(ElementRef);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  private readonly destroy$ = new Subject<void>();

  readonly isMobile = signal(false);
  readonly mobileMenuOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly settingsMenuOpen = signal(false);
  readonly navbarHidden = signal(false);

  /**
   * Mirrors {@link navbarHidden} onto the root element so a PAGE can lay itself
   * out against the navbar that is actually on screen. The bar hides on
   * scroll-down, and a page that reserves its height unconditionally leaves a
   * strip of nothing at the top — the profile rail centred itself in "viewport
   * minus a navbar" that was not there. CSS-only alternatives do not reach:
   * the bar is a sibling of the router outlet, so no selector gets from one to
   * the other. -> _home-design.scss --cl-nav-offset
   */
  private readonly syncNavbarVisibilityClass = effect(() => {
    const hidden = this.navbarHidden();
    if (!this.isBrowser) return;
    document.documentElement.classList.toggle('cl-nav-hidden', hidden);
  });
  readonly navigating = signal(false);
  private lastScrollY = 0;
  private readonly scrollThreshold = 10;
  // Reactive session flag from the service; transitions drive the
  // loadCustomerUser dispatch below.
  readonly isLoggedIn = this.authService.isLoggedIn;
  readonly isDarkMode = computed(() => this.themeService.currentTheme() === 'dark');

  private readonly currentUser = toSignal(
    this.store.select(selectCustomerCurrentUser),
    { initialValue: undefined }
  );

  readonly userDisplayName = computed(() => {
    const user = this.currentUser();
    if (user?.firstName || user?.lastName) {
      return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim();
    }
    return null;
  });

  readonly userEmail = computed(() => this.currentUser()?.email ?? null);

  readonly userInitials = computed(() => {
    const user = this.currentUser();
    if (user?.firstName && user?.lastName) {
      return `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();
    }
    return null;
  });

  readonly isEmailConfirmed = computed(() => this.currentUser()?.isEmailConfirmed ?? false);
  readonly userProfileType = computed(() => this.currentUser()?.profile?.name ?? null);

  constructor() {
    if (this.isBrowser) {
      this.updateMobileStatus();
    }

    // Refresh the cached user whenever the session flips to logged-in.
    // Runs once at construction with the seed value too — if the user lands
    // already authenticated (page refresh), the store gets populated.
    effect(() => {
      if (this.authService.isLoggedIn()) {
        this.store.dispatch(loadCustomerUser());
      }
    });
  }

  ngOnInit(): void {
    this.router.events
      .pipe(takeUntil(this.destroy$))
      .subscribe((event) => {
        if (event instanceof NavigationStart) {
          this.navigating.set(true);
        }
        if (
          event instanceof NavigationEnd ||
          event instanceof NavigationCancel ||
          event instanceof NavigationError
        ) {
          this.navigating.set(false);
          this.mobileMenuOpen.set(false);
          this.userMenuOpen.set(false);
          this.settingsMenuOpen.set(false);
        }
      });

  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('window:resize')
  onResize(): void {
    if (!this.isBrowser) return;
    this.updateMobileStatus();
  }

  @HostListener('window:scroll')
  onScroll(): void {
    if (!this.isBrowser) return;
    const currentScrollY = window.scrollY;
    if (Math.abs(currentScrollY - this.lastScrollY) < this.scrollThreshold) {
      return;
    }
    if (currentScrollY > this.lastScrollY && currentScrollY > 64) {
      // Scrolling down & past the navbar height — hide
      this.navbarHidden.set(true);
      this.mobileMenuOpen.set(false);
      this.userMenuOpen.set(false);
      this.settingsMenuOpen.set(false);
    } else {
      // Scrolling up — show
      this.navbarHidden.set(false);
    }
    this.lastScrollY = currentScrollY;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elRef.nativeElement.contains(event.target)) {
      this.userMenuOpen.set(false);
      this.settingsMenuOpen.set(false);
    }
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((v) => !v);
    this.userMenuOpen.set(false);
    this.settingsMenuOpen.set(false);
  }

  toggleUserMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.userMenuOpen.update((v) => !v);
    this.settingsMenuOpen.set(false);
  }

  toggleSettingsMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.settingsMenuOpen.update((v) => !v);
    this.userMenuOpen.set(false);
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  navigateTo(route: string): void {
    this.router.navigate([route]);
    this.mobileMenuOpen.set(false);
    this.userMenuOpen.set(false);
  }

  // Links are real routerLink anchors (crawlable hrefs); this only has to
  // cover the same-URL click, where no NavigationEnd fires to close menus.
  /**
   * The one nav slot that changes meaning with the session: a signed-out
   * visitor tracks an order, a signed-in one opens their list. Computed rather
   * than branched in the template so the bar renders the same node either way.
   */
  readonly ordersLink = computed(() => (this.isLoggedIn() ? '/orders' : '/track-order'));

  /**
   * `/membership` is behind `customerAuthGuard`, so this link used to send
   * every anonymous visitor who clicked "Cleansia Plus" to a login form with
   * no explanation of what they had clicked. Signed in it still opens the
   * management screen; signed out it opens the public page that argues for it.
   *
   * An attribute, not an `@if` — same reason as `ordersLink` above.
   */
  readonly plusLink = computed(() => (this.isLoggedIn() ? '/membership' : '/plus'));
  readonly ordersLabel = computed(() => (this.isLoggedIn() ? 'nav.my_orders' : 'nav.track_order'));

  closeMenus(): void {
    this.mobileMenuOpen.set(false);
    this.userMenuOpen.set(false);
    this.settingsMenuOpen.set(false);
  }

  logout(): void {
    this.userMenuOpen.set(false);
    this.dialogService
      .confirmTranslated(
        'global.dialog.confirm_logout',
        'global.dialog.confirm'
      )
      .subscribe((confirmed) => {
        if (confirmed) {
          // logout() returns a cold Observable — must subscribe or nothing
          // happens (no server-side refresh-token revoke, no local cookie
          // cleanup, no redirect). The pipe(tap(...)) inside the service does the work.
          this.authService.logout().subscribe();
        }
      });
  }

  private updateMobileStatus(): void {
    this.isMobile.set(window.innerWidth < NAV_DESKTOP_MIN_WIDTH);
  }
}
