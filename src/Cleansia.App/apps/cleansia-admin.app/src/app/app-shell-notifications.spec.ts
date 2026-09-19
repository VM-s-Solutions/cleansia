import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { readFileSync } from 'fs';
import { join } from 'path';
import { AdminAuthService, AdminNotificationBadgeService } from '@cleansia/admin-services';
import { DialogService, PageTitleService, Policy } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, EMPTY } from 'rxjs';
import { AppComponent } from './app.component';

/**
 * The shell owns no notification logic: it starts the badge poll once the browser is up and draws
 * whatever label the badge service answers on the one sidebar entry that is the bell. Every other
 * entry keeps its identity across a badge change, because the sidebar writes `expanded` onto the
 * item objects and a rebuilt array would fold every open submenu.
 */
describe('admin app shell notifications', () => {
  let badgeLabel: ReturnType<typeof signal<string | null>>;
  let start: jest.Mock;

  function create(): AppComponent {
    const component = TestBed.createComponent(AppComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    badgeLabel = signal<string | null>(null);
    start = jest.fn();

    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: Store, useValue: { dispatch: jest.fn() } },
        {
          provide: AdminAuthService,
          useValue: { isLoggedIn$: new BehaviorSubject(true), logout: () => EMPTY },
        },
        { provide: PageTitleService, useValue: { initialize: jest.fn() } },
        { provide: AdminNotificationBadgeService, useValue: { start, badgeLabel } },
        { provide: DialogService, useValue: { confirmTranslated: () => EMPTY } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    }).overrideComponent(AppComponent, { set: { template: '', imports: [] } });
  });

  it('starts the badge poll when the shell comes up in a browser', () => {
    create();

    expect(start).toHaveBeenCalledTimes(1);
  });

  it('lists notifications first in the sidebar, gated on the feed policy, with no badge at zero', () => {
    const [first] = create().sidebarMenuItems();

    expect(first.label).toBe('sidebar.notifications');
    expect(first.route).toBe('/notifications');
    expect(first.icon).toBe('pi pi-bell');
    expect(first.permission).toBe(Policy.CanViewAdminNotifications);
    expect(first.badge).toBeUndefined();
  });

  it('stamps the badge label on the notifications entry only, keeping every other entry by identity', () => {
    const component = create();
    const before = component.sidebarMenuItems();

    badgeLabel.set('3');
    const after = component.sidebarMenuItems();

    expect(after[0].badge).toBe('3');
    expect(after[0]).not.toBe(before[0]);
    expect(after.slice(1)).toEqual(before.slice(1));
    for (let i = 1; i < before.length; i++) {
      expect(after[i]).toBe(before[i]);
    }

    badgeLabel.set(null);
    expect(component.sidebarMenuItems()[0].badge).toBeUndefined();
  });

  it('draws the mobile bell beside the language switcher, behind the same policy, with the count off the badge service', () => {
    const html = readFileSync(join(__dirname, 'app.component.html'), 'utf-8');

    expect(html).toMatch(/\*cleansiaPermission="Policy\.CanViewAdminNotifications"/);
    expect(html).toMatch(/icon="pi pi-bell"[\s\S]*\[routerLink\]="notificationsRoute"/);
    expect(html).toMatch(/@if \(notificationBadge\.badgeLabel\(\); as badge\)/);
    expect(html).toMatch(/mobile-toolbar__bell[\s\S]*<cleansia-language-switcher \/>/);
    expect(html).toMatch(/\[menuItems\]="sidebarMenuItems\(\)"/);
  });
});
