import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import {
  GetLoyaltyActivityActivityItem,
  GetMyLoyaltyResponse,
  LoyaltyEarnSource,
  LoyaltyTransactionType,
} from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { RewardsActivityComponent } from './rewards-activity.component';
import { RewardsFacade } from './rewards.facade';

describe('RewardsActivityComponent — what a points movement says it was', () => {
  const movement = (type: LoyaltyTransactionType, source: LoyaltyEarnSource, points: number) =>
    GetLoyaltyActivityActivityItem.fromJS({
      type,
      source,
      points,
      orderDisplayNumber: 'CL-2026-0301',
      occurredOn: '2026-10-01T10:00:00Z',
    });

  async function render(activity: GetLoyaltyActivityActivityItem[]): Promise<string[]> {
    const facade = {
      loadAll: jest.fn(),
      loadActivityPage: jest.fn(),
      account: signal(GetMyLoyaltyResponse.fromJS({ currentTier: 2, lifetimePoints: 150, points: 150 })),
      activityList: signal(activity),
      activityLoading: signal(false),
      loadingMore: signal(false),
      totalActivity: signal(activity.length),
    };

    await TestBed.configureTestingModule({
      imports: [RewardsActivityComponent, TranslateModule.forRoot()],
      providers: [provideNoopAnimations(), { provide: RewardsFacade, useValue: facade }],
    }).compileComponents();

    const fixture = TestBed.createComponent(RewardsActivityComponent);
    fixture.detectChanges();
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cl-rwd__move-name'),
    ).map((el) => el.textContent?.trim() ?? '');
  }

  it('reads a refund clawback as a refund, not as a cancelled booking', async () => {
    const labels = await render([
      movement(LoyaltyTransactionType.Revoke, LoyaltyEarnSource.OrderPartiallyRefunded, -12),
      movement(LoyaltyTransactionType.Revoke, LoyaltyEarnSource.OrderCancelled, -170),
      movement(LoyaltyTransactionType.Revoke, LoyaltyEarnSource.ManualRevoke, -20),
      movement(LoyaltyTransactionType.Earn, LoyaltyEarnSource.ManualGrant, 50),
    ]);

    expect(labels).toEqual([
      'pages.rewards.tx.refunded',
      'pages.rewards.tx.cancelled',
      'pages.rewards.tx.manual',
      'pages.rewards.tx.manual',
    ]);
  });
});
