import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, AdminNotificationBadgeService, UserNotificationDto } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  buildMarkAllReadCommand,
  buildMarkReadCommand,
  buildNotificationRows,
  newestCreatedOn,
  NOTIFICATIONS_PAGE_SIZE,
  NotificationRow,
} from './notifications.models';

const PAGE = 'pages.notifications';

@Injectable()
export class NotificationsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly badge = inject(AdminNotificationBadgeService);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);
  readonly markingAll = signal<boolean>(false);
  readonly openingId = signal<string | null>(null);
  readonly first = signal<number>(0);
  readonly pageSize = NOTIFICATIONS_PAGE_SIZE;

  private readonly items = signal<UserNotificationDto[]>([]);
  private readonly language = signal<string>(this.translate.currentLang);

  // The newest createdOn ever fetched: the mark-all watermark, so a row that lands after the fetch
  // stays unread, and a later page never narrows it to its own older rows.
  private newestFetched: Date | undefined;

  readonly rows = computed<NotificationRow[]>(() =>
    buildNotificationRows(this.items(), this.language(), (key) => this.translate.instant(key))
  );

  readonly hasUnread = computed(() => this.badge.unreadCount() > 0 || this.rows().some((row) => row.isUnread));

  constructor() {
    super();
    this.translate.onLangChange
      .pipe(takeUntil(this.destroyed$))
      .subscribe((event) => this.language.set(event.lang));
  }

  load(): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.adminClient.adminNotificationClient
      .getPaged(undefined, undefined, this.first(), this.pageSize)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          const data = response.data ?? [];
          this.items.set(data);
          this.totalRecords.set(response.total ?? 0);
          const newest = newestCreatedOn(data);
          if (newest && (!this.newestFetched || newest > this.newestFetched)) this.newestFetched = newest;
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(first: number): void {
    this.first.set(first);
    this.load();
  }

  open(row: NotificationRow): void {
    if (this.openingId()) return;
    if (!row.isUnread) {
      this.navigateTo(row);
      return;
    }

    this.openingId.set(row.id);
    this.adminClient.adminNotificationClient
      .markRead(buildMarkReadCommand(row.id))
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.openingId.set(null))
      )
      .subscribe((response) => {
        if (response) this.badge.refresh();
        if (row.route) {
          this.navigateTo(row);
        } else {
          this.load();
        }
      });
  }

  markAllRead(): void {
    if (this.markingAll() || !this.newestFetched) return;

    this.markingAll.set(true);
    this.adminClient.adminNotificationClient
      .markAllRead(buildMarkAllReadCommand(this.newestFetched))
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.markingAll.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant(`${PAGE}.messages.marked_all_read`, { count: response.markedCount ?? 0 })
          );
          this.badge.refresh();
        }
        this.load();
      });
  }

  private navigateTo(row: NotificationRow): void {
    if (row.route) this.router.navigate(row.route);
  }
}
