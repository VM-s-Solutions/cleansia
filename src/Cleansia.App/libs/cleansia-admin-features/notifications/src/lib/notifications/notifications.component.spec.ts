import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminNotificationBadgeService,
  MarkNotificationReadResponse,
  PagedDataOfUserNotificationDto,
  UserNotificationDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { NotificationsComponent } from './notifications.component';
import { NOTIFICATIONS_PAGE_SIZE } from './notifications.models';

function item(overrides: Record<string, unknown> = {}): UserNotificationDto {
  return UserNotificationDto.fromJS({
    id: 'n-1',
    eventKey: 'admin.dispute.filed',
    args: { orderNumber: 'ORD-1', reason: 'ServiceQuality', disputeId: 'd-1', orderId: 'o-1' },
    createdOn: '2026-09-19T08:30:00Z',
    ...overrides,
  });
}

function page(items: UserNotificationDto[], total = items.length): PagedDataOfUserNotificationDto {
  return PagedDataOfUserNotificationDto.fromJS({ pageNumber: 1, pageSize: NOTIFICATIONS_PAGE_SIZE, total, data: items.map((i) => i.toJSON()) });
}

describe('NotificationsComponent', () => {
  let fixture: ComponentFixture<NotificationsComponent>;
  let getPaged: jest.Mock;
  let markRead: jest.Mock;
  let markAllRead: jest.Mock;
  let navigate: jest.Mock;
  let refreshBadge: jest.Mock;

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent ?? '';
  const rows = () => Array.from(element().querySelectorAll('.cleansia-notifications__row')) as HTMLElement[];
  const markAllButton = () =>
    element().querySelector('.cleansia-notifications__header cleansia-button button') as HTMLButtonElement;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [NotificationsComponent, TranslateModule.forRoot()],
      providers: [
        { provide: AdminClient, useValue: { adminNotificationClient: { getPaged, markRead, markAllRead } } },
        { provide: AdminNotificationBadgeService, useValue: { refresh: refreshBadge } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn() } },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationsComponent);
    fixture.detectChanges();
  }

  beforeEach(() => {
    getPaged = jest.fn().mockReturnValue(of(page([item(), item({ id: 'n-2', readOn: '2026-09-19T09:00:00Z' })])));
    markRead = jest.fn().mockReturnValue(of(MarkNotificationReadResponse.fromJS({ id: 'n-1', readOn: '2026-09-19T09:30:00Z' })));
    markAllRead = jest.fn();
    navigate = jest.fn().mockResolvedValue(true);
    refreshBadge = jest.fn();
  });

  it('shows the loader until the feed arrives', async () => {
    getPaged.mockReturnValue(new Subject<PagedDataOfUserNotificationDto>());

    await render();

    expect(element().querySelector('cleansia-loader')).not.toBeNull();
    expect(rows()).toHaveLength(0);
  });

  it('renders the error state with a retry that re-reads', async () => {
    getPaged.mockReturnValueOnce(throwError(() => new Error('boom'))).mockReturnValueOnce(of(page([item()])));

    await render();

    expect(text()).toContain('pages.notifications.load_error');
    (element().querySelector('.cleansia-notifications__state cleansia-button button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(getPaged).toHaveBeenCalledTimes(2);
    expect(rows()).toHaveLength(1);
  });

  it('renders the empty state and disables mark-all when there is nothing to read', async () => {
    getPaged.mockReturnValue(of(page([])));

    await render();

    expect(text()).toContain('pages.notifications.empty');
    expect(rows()).toHaveLength(0);
    expect(markAllButton().disabled).toBe(true);
  });

  it('renders a row per notification with its sentence, its stamp, and the unread emphasis', async () => {
    await render();

    expect(rows()).toHaveLength(2);
    expect(rows()[0].classList).toContain('cleansia-notifications__row--unread');
    expect(rows()[0].classList).toContain('cleansia-notifications__row--linked');
    expect(rows()[1].classList).not.toContain('cleansia-notifications__row--unread');
    expect(rows()[0].textContent).toContain('pages.notifications.events.admin.dispute.filed.title');
    expect(rows()[0].textContent).toContain('pages.notifications.events.admin.dispute.filed.body');
    expect(rows()[0].querySelector('time')?.getAttribute('datetime')).toBe('2026-09-19T08:30:00.000Z');
    expect(rows()[0].getAttribute('role')).toBe('button');
    expect(rows()[0].getAttribute('tabindex')).toBe('0');
    expect(markAllButton().disabled).toBe(false);
    expect(element().querySelector('p-paginator')).toBeNull();
  });

  it('opens a row on click: marks it read and lands on the dispute', async () => {
    await render();

    rows()[0].click();
    fixture.detectChanges();

    expect(markRead).toHaveBeenCalledTimes(1);
    expect(refreshBadge).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith(['dispute-management', 'd-1']);
  });

  it('opens a row from the keyboard too', async () => {
    await render();

    rows()[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(markRead).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['dispute-management', 'd-1']);
  });

  it('pages when the feed is longer than one page', async () => {
    getPaged.mockReturnValue(of(page([item()], NOTIFICATIONS_PAGE_SIZE * 2)));

    await render();

    expect(element().querySelector('p-paginator')).not.toBeNull();
  });
});
