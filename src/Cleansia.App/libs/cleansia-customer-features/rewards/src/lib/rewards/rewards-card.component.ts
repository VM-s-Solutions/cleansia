import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { RewardsFacade } from './rewards.facade';

/**
 * Compact loyalty summary used as the discovery surface on the Profile page.
 * Pulls from the shared `RewardsFacade` so visiting Profile pre-warms the
 * Rewards page cache.
 */
@Component({
  selector: 'cleansia-customer-rewards-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, TranslatePipe, SkeletonModule],
  templateUrl: './rewards-card.component.html',
})
export class RewardsCardComponent implements OnInit {
  protected readonly facade = inject(RewardsFacade);

  ngOnInit(): void {
    if (!this.facade.hasLoaded()) {
      this.facade.loadAll();
    }
  }
}
