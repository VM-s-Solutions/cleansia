import { ChangeDetectionStrategy, Component, inject, OnDestroy, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { IsActiveMatchOptions, NavigationEnd, Router } from '@angular/router';
import { AdminNotificationBadgeService } from '@cleansia/admin-services';
import { CleansiaButtonComponent, CleansiaLoaderComponent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { Popover } from 'primeng/popover';
import { filter, map, takeUntil } from 'rxjs';
import { NotificationFeedComponent } from '../notification-feed/notification-feed.component';
import { NotificationsFacade } from '../notifications/notifications.facade';

const FEED_ROUTE = `/${CleansiaAdminRoute.NOTIFICATIONS}`;
const FEED_PAGE: IsActiveMatchOptions = {
  paths: 'exact',
  queryParams: 'ignored',
  fragment: 'ignored',
  matrixParams: 'ignored',
};

@Component({
  selector: 'cleansia-admin-notifications-launcher',
  standalone: true,
  imports: [TranslatePipe, Popover, CleansiaButtonComponent, CleansiaLoaderComponent, NotificationFeedComponent],
  templateUrl: './notifications-launcher.component.html',
  providers: [NotificationsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminNotificationsLauncherComponent extends UnsubscribeControlDirective implements OnDestroy {
  protected readonly facade = inject(NotificationsFacade);
  protected readonly badge = inject(AdminNotificationBadgeService);
  private readonly router = inject(Router);
  private readonly panel = viewChild(Popover);

  protected readonly feedRoute = FEED_ROUTE;
  protected readonly panelId = 'cleansia-notifications-panel';
  readonly panelOpen = signal(false);

  private readonly navigationEnds = this.router.events.pipe(filter((event) => event instanceof NavigationEnd));
  protected readonly onFeedPage = toSignal(this.navigationEnds.pipe(map(() => this.isOnFeedPage())), {
    initialValue: this.isOnFeedPage(),
  });

  private panelResize?: ResizeObserver;

  constructor() {
    super();
    this.navigationEnds.pipe(takeUntil(this.destroyed$)).subscribe(() => this.panel()?.hide());
  }

  toggle(event: Event): void {
    this.panel()?.toggle(event);
  }

  protected onPanelShow(): void {
    this.panelOpen.set(true);
    this.facade.load();
    this.followPanelSize();
  }

  protected onPanelHide(): void {
    this.panelOpen.set(false);
    this.panelResize?.disconnect();
  }

  override ngOnDestroy(): void {
    this.panelResize?.disconnect();
    super.ngOnDestroy();
  }

  // The panel is placed once, when it opens, from its height at that moment; the feed lands after
  // that, so it is re-placed whenever its size changes or it would grow down over its own bell.
  private followPanelSize(): void {
    const panel = this.panel();
    if (!panel?.container) return;
    this.panelResize?.disconnect();
    this.panelResize = new ResizeObserver(() => panel.align());
    this.panelResize.observe(panel.container);
  }

  private isOnFeedPage(): boolean {
    return this.router.isActive(FEED_ROUTE, FEED_PAGE);
  }
}
