import {
  ChangeDetectionStrategy,
  Component,
  computed,
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

import { CleansiaBrandNameComponent } from '../cleansia-brand-name';
import { CleansiaLanguageSwitcherComponent } from '../cleansia-language-switcher';

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
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './cleansia-customer-navbar.component.html',
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
  readonly navigating = signal(false);
  private lastScrollY = 0;
  private readonly scrollThreshold = 10;
  private readonly isLoggedInSignal = signal(this.authService.isLoggedIn());

  readonly isLoggedIn = computed(() => this.isLoggedInSignal());
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

    this.authService.isLoggedIn$
      .pipe(takeUntil(this.destroy$))
      .subscribe((value) => {
        this.isLoggedInSignal.set(value);
        if (value) {
          this.store.dispatch(loadCustomerUser());
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
    this.isMobile.set(window.innerWidth < 768);
  }
}
