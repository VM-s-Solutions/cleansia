import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import {
  CleansiaLabelComponent,
} from '@cleansia/components';
import { ProductivityMetricsDto } from '@cleansia/partner-services';
import { currentLanguage, formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-productivity-gauges',
  standalone: true,
  imports: [
    CommonModule,
    TranslateModule,
    Skeleton,
    CleansiaLabelComponent,
  ],
  templateUrl: './cleansia-productivity-gauges.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaProductivityGaugesComponent {
  private readonly translate = inject(TranslateService);

  data = input<ProductivityMetricsDto | null>(null);
  loading = input<boolean>(false);
  /**
   * The currency the amounts are denominated in, from the server. Never assumed: the cleaner's
   * currency derives from their approved work country, and a hardcoded symbol here would disagree
   * with their payout invoice the day a second country configuration exists. An absent code renders
   * the number with no symbol rather than guessing one. → /flows/pay-and-payouts
   */
  currencyCode = input<string | undefined>(undefined);

  private readonly lang = currentLanguage(this.translate);

  protected readonly highestEarning = computed(() =>
    formatMoney(
      this.data()?.personalBests?.highestEarningMonth?.amount ?? 0,
      this.currencyCode(),
      localeFor(this.lang())
    )
  );

  protected readonly formattedMostOrdersDate = computed(() =>
    formatDate(this.data()?.personalBests?.mostOrdersDate, this.lang())
  );

  getArcPath(percentage: number): string {
    const radius = 80;
    const centerX = 100;
    const centerY = 100;

    const clampedPercentage = Math.max(0, Math.min(100, percentage));

    const startAngle = -90;
    const endAngle = -90 + (clampedPercentage / 100) * 180;

    const start = this.polarToCartesian(centerX, centerY, radius, endAngle);
    const end = this.polarToCartesian(centerX, centerY, radius, startAngle);

    const largeArcFlag = clampedPercentage > 50 ? 1 : 0;

    return [
      'M',
      start.x,
      start.y,
      'A',
      radius,
      radius,
      0,
      largeArcFlag,
      0,
      end.x,
      end.y,
    ].join(' ');
  }

  private polarToCartesian(
    centerX: number,
    centerY: number,
    radius: number,
    angleInDegrees: number
  ) {
    const angleInRadians = ((angleInDegrees - 90) * Math.PI) / 180.0;
    return {
      x: centerX + radius * Math.cos(angleInRadians),
      y: centerY + radius * Math.sin(angleInRadians),
    };
  }
}
