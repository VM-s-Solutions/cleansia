import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  GetUserLoyaltyAccountResponse,
  GetUserLoyaltyActivityActivityItem,
  GrantPointsManuallyCommand,
  RevokePointsManuallyCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface ManualPointsInput {
  points: number;
  reason: string;
}

@Injectable()
export class UserLoyaltyDetailFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly account = signal<GetUserLoyaltyAccountResponse | null>(null);
  readonly accountLoading = signal<boolean>(false);

  readonly activity = signal<GetUserLoyaltyActivityActivityItem[]>([]);
  readonly activityLoading = signal<boolean>(false);
  readonly activityTotal = signal<number>(0);

  readonly submitting = signal<boolean>(false);

  private currentUserId: string | null = null;
  private currentActivityOffset = 0;
  private currentActivityLimit = 20;

  loadAccount(userId: string): void {
    this.currentUserId = userId;
    this.accountLoading.set(true);
    this.adminClient.adminLoyaltyClient
      .userAccount(userId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.accountLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.account.set(response);
        }
      });
  }

  loadActivity(userId: string, offset = 0, limit = 20): void {
    this.currentUserId = userId;
    this.currentActivityOffset = offset;
    this.currentActivityLimit = limit;
    this.activityLoading.set(true);
    this.adminClient.adminLoyaltyClient
      .userActivity(userId, offset, limit)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.activityLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.activity.set(response.data ?? []);
          this.activityTotal.set(response.total ?? 0);
        }
      });
  }

  onActivityPageChange(offset: number, limit: number): void {
    if (!this.currentUserId) return;
    this.loadActivity(this.currentUserId, offset, limit);
  }

  refresh(): void {
    if (!this.currentUserId) return;
    this.loadAccount(this.currentUserId);
    this.loadActivity(
      this.currentUserId,
      this.currentActivityOffset,
      this.currentActivityLimit
    );
  }

  grantPoints(input: ManualPointsInput, onSuccess?: () => void): void {
    if (!this.currentUserId) return;
    this.submitting.set(true);

    const command = new GrantPointsManuallyCommand({
      userId: this.currentUserId,
      points: input.points,
      reason: input.reason,
      // ActorId is enriched server-side from the JWT.
      actorId: undefined,
    });

    this.adminClient.adminLoyaltyClient
      .grantPoints(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.error.generic'
            )
          );
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.success_grant'
            )
          );
          this.refresh();
          onSuccess?.();
        }
      });
  }

  revokePoints(input: ManualPointsInput, onSuccess?: () => void): void {
    if (!this.currentUserId) return;
    this.submitting.set(true);

    const command = new RevokePointsManuallyCommand({
      userId: this.currentUserId,
      points: input.points,
      reason: input.reason,
      actorId: undefined,
    });

    this.adminClient.adminLoyaltyClient
      .revokePoints(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.error.generic'
            )
          );
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.success_revoke'
            )
          );
          this.refresh();
          onSuccess?.();
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/admin-user-management']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
