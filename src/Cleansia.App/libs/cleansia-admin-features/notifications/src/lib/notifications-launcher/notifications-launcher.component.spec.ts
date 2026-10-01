import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import {
  AdminClient,
  AdminNotificationBadgeService,
  MarkAllNotificationsReadResponse,
  MarkNotificationReadResponse,
  PagedDataOfUserNotificationDto,
  UserNotificationDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { Popover } from 'primeng/popover';
import { of } from 'rxjs';
import { NOTIFICATIONS_PAGE_SIZE } from '../notifications/notifications.models';
import { AdminNotificationsLauncherComponent } from './notifications-launcher.component';

@Component({ template: '' })
class BlankPageComponent {}

class ResizeObserverStub {
  static instances: ResizeObserverStub[] = [];
  readonly observe = jest.fn();
  readonly disconnect = jest.fn();

  constructor(private readonly callback: ResizeObserverCallback) {
    ResizeObserverStub.instances.push(this);
  }

  resize(): void {
    this.callback([], this as unknown as ResizeObserver);
  }
}

function item(overrides: Record<string, unknown> = {}): UserNotificationDto {
  return UserNotificationDto.fromJS({
    id: 'n-1',
    eventKey: 'admin.dispute.filed',
    args: { orderNumber: 'ORD-1', reason: 'ServiceQuality', disputeId: 'd-1', orderId: 'o-1' },
    createdOn: '2026-09-19T08:30:00Z',
    ...overrides,
  });
}

function page(items: UserNotificationDto[]): PagedDataOfUserNotificationDto {
  return PagedDataOfUserNotificationDto.fromJS({
    pageNumber: 1,
    pageSize: NOTIFICATIONS_PAGE_SIZE,
    total: items.length,
    data: items.map((i) => i.toJSON()),
  });
}

describe('AdminNotificationsLauncherComponent', () => {
  const globals = globalThis as unknown as Record<string, unknown>;
  const originalResizeObserver = globals['ResizeObserver'];

  let fixture: ComponentFixture<AdminNotificationsLauncherComponent>;
  let router: Router;
  let getPaged: jest.Mock;
  let markRead: jest.Mock;
  let markAllRead: jest.Mock;
  let refreshBadge: jest.Mock;
  let badgeLabel: ReturnType<typeof signal<string | null>>;

  const host = () => fixture.nativeElement as HTMLElement;
  const bell = () => host().querySelector('.cleansia-notifications-launcher button') as HTMLButtonElement | null;
  const count = () => host().querySelector('.cleansia-notifications-launcher__count') as HTMLElement | null;
  const panel = () => document.body.querySelector('.cleansia-notifications-panel') as HTMLElement | null;
  const rows = () => Array.from(panel()?.querySelectorAll('.cleansia-notifications__row') ?? []) as HTMLElement[];
  const popover = () => fixture.debugElement.query(By.directive(Popover)).componentInstance as Popover;

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function render(url = '/order-management'): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AdminNotificationsLauncherComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([{ path: '**', component: BlankPageComponent }]),
        { provide: AdminClient, useValue: { adminNotificationClient: { getPaged, markRead, markAllRead } } },
        {
          provide: AdminNotificationBadgeService,
          useValue: { badgeLabel, unreadCount: signal(1), refresh: refreshBadge },
        },
        { provide: SnackbarService, useValue: { showSuccessTranslated: jest.fn() } },
      ],
    });
    router = TestBed.inject(Router);
    await router.navigateByUrl(url);
    fixture = TestBed.createComponent(AdminNotificationsLauncherComponent);
    await settle();
  }

  async function openPanel(): Promise<void> {
    bell()?.click();
    await settle();
  }

  beforeEach(() => {
    ResizeObserverStub.instances = [];
    globals['ResizeObserver'] = ResizeObserverStub;
    getPaged = jest.fn().mockReturnValue(of(page([item(), item({ id: 'n-2', readOn: '2026-09-19T09:00:00Z' })])));
    markRead = jest.fn().mockReturnValue(of(MarkNotificationReadResponse.fromJS({ id: 'n-1', readOn: '2026-09-19T09:30:00Z' })));
    markAllRead = jest.fn().mockReturnValue(of(MarkAllNotificationsReadResponse.fromJS({ markedCount: 1 })));
    refreshBadge = jest.fn();
    badgeLabel = signal<string | null>('3');
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    document.body.querySelectorAll('.p-popover').forEach((element) => element.remove());
    globals['ResizeObserver'] = originalResizeObserver;
  });

  it('draws the bell with the unread count off the badge service, and no count at zero', async () => {
    await render();

    expect(bell()?.getAttribute('aria-label')).toBe('components.notifications_panel.open');
    expect(count()?.textContent?.trim()).toBe('3');

    badgeLabel.set(null);
    await settle();

    expect(bell()).not.toBeNull();
    expect(count()).toBeNull();
  });

  it('opens the panel on the bell and reads the latest notifications into it', async () => {
    await render();
    expect(getPaged).not.toHaveBeenCalled();

    await openPanel();

    expect(popover().overlayVisible).toBe(true);
    expect(getPaged).toHaveBeenCalledTimes(1);
    expect(panel()?.textContent).toContain('components.notifications_panel.title');
    expect(rows()).toHaveLength(2);
    expect(bell()?.getAttribute('aria-expanded')).toBe('true');
  });

  it('opening a notification marks it read, lands on it and closes the panel', async () => {
    await render();
    await openPanel();

    rows()[0].click();
    await settle();

    expect(markRead).toHaveBeenCalledTimes(1);
    expect(markRead.mock.calls[0][0].id).toBe('n-1');
    expect(refreshBadge).toHaveBeenCalledTimes(1);
    expect(router.url).toBe('/dispute-management/d-1');
    expect(popover().overlayVisible).toBe(false);
  });

  it('marks everything read from the panel and links to the whole feed', async () => {
    await render();
    await openPanel();

    (panel()?.querySelector('.cleansia-notifications-panel__head button') as HTMLButtonElement).click();
    await settle();

    expect(markAllRead).toHaveBeenCalledTimes(1);
    expect(refreshBadge).toHaveBeenCalledTimes(1);
    expect(panel()?.querySelector('a[href="/notifications"]')?.textContent).toContain('components.notifications_panel.view_all');
  });

  it('says so when there is nothing in the feed', async () => {
    getPaged.mockReturnValue(of(page([])));
    await render();
    await openPanel();

    expect(rows()).toHaveLength(0);
    expect(panel()?.querySelector('.empty-state')?.textContent).toContain('components.notifications_panel.empty');
  });

  it('keeps the panel on its bell while its content grows, and lets go when it closes', async () => {
    await render();
    await openPanel();
    const [observer] = ResizeObserverStub.instances;
    expect(observer.observe).toHaveBeenCalledWith(popover().container);
    const align = jest.spyOn(popover(), 'align');

    observer.resize();
    expect(align).toHaveBeenCalledTimes(1);

    bell()?.click();
    await settle();
    expect(observer.disconnect).toHaveBeenCalled();
  });

  it('leaves the bell off the notifications page, where the whole feed is already open', async () => {
    await render('/notifications');
    expect(bell()).toBeNull();

    await router.navigateByUrl('/order-management');
    await settle();
    expect(bell()).not.toBeNull();

    await router.navigateByUrl('/notifications?page=2');
    await settle();
    expect(bell()).toBeNull();
  });
});
