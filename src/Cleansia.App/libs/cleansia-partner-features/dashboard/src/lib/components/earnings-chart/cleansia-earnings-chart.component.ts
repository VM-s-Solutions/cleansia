import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  ViewChild,
} from '@angular/core';
import {
  CleansiaLabelComponent,
} from '@cleansia/components';
import { EarningsAnalyticsDto } from '@cleansia/partner-services';
import { currentLanguage, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartConfiguration, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { Skeleton } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-earnings-chart',
  standalone: true,
  imports: [
    CommonModule,
    BaseChartDirective,
    TranslateModule,
    Skeleton,
    CleansiaLabelComponent,
  ],
  templateUrl: './cleansia-earnings-chart.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaEarningsChartComponent {
  private readonly translate = inject(TranslateService);

  data = input<EarningsAnalyticsDto | null>(null);
  loading = input<boolean>(false);
  /**
   * The currency the amounts are denominated in, from the server. Never assumed: the cleaner's
   * currency derives from their approved work country, and a hardcoded symbol here would disagree
   * with their payout invoice the day a second country configuration exists. An absent code renders
   * the number with no symbol rather than guessing one. → /flows/pay-and-payouts
   */
  currencyCode = input<string | undefined>(undefined);

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  private readonly lang = currentLanguage(this.translate);

  readonly totalEarnings = computed(() => this.money(this.data()?.totalEarnings));
  readonly averageMonthlyEarnings = computed(() => this.money(this.data()?.averageMonthlyEarnings));

  lineChartType: ChartType = 'line';
  lineChartData: ChartConfiguration['data'] = {
    datasets: [],
    labels: [],
  };

  lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top',
      },
      tooltip: {
        mode: 'index',
        intersect: false,
        callbacks: {
          label: (context) => `${context.dataset.label || ''}: ${this.money(context.parsed.y)}`,
        },
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          callback: (value) => this.money(Number(value)),
        },
      },
    },
  };

  constructor() {
    effect(() => {
      const currentData = this.data();
      if (currentData) {
        this.updateChartData(currentData);
      }
    });
  }

  private money(value: number | null | undefined): string {
    return formatMoney(value ?? 0, this.currencyCode(), localeFor(this.lang()));
  }

  private updateChartData(currentData: EarningsAnalyticsDto): void {
    if (!currentData?.monthlyEarnings) {
      return;
    }

    const labels = currentData.monthlyEarnings.map((m) => m.monthName);
    const earnings = currentData.monthlyEarnings.map((m) => m.amount);

    this.lineChartData = {
      labels,
      datasets: [
        {
          data: earnings,
          label: this.translate.instant('pages.dashboard.earnings_analytics.chart_label'),
          borderColor: '#8b5cf6',
          backgroundColor: 'rgba(139, 92, 246, 0.1)',
          fill: true,
          tension: 0.4,
          pointRadius: 4,
          pointHoverRadius: 6,
          pointBackgroundColor: '#8b5cf6',
          pointBorderColor: '#fff',
          pointBorderWidth: 2,
        },
      ],
    };

    this.chart?.update();
  }
}
