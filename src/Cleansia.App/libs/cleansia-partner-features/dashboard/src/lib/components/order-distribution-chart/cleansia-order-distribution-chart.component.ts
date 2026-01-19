import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  input,
  ViewChild,
} from '@angular/core';
import {
  CleansiaLabelComponent,
  CleansiaLoaderComponent,
} from '@cleansia/components';
import { OrderAnalyticsDto } from '@cleansia/partner-services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartConfiguration, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';

@Component({
  selector: 'cleansia-order-distribution-chart',
  standalone: true,
  imports: [
    CommonModule,
    BaseChartDirective,
    TranslateModule,
    CleansiaLoaderComponent,
    CleansiaLabelComponent,
  ],
  templateUrl: './cleansia-order-distribution-chart.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaOrderDistributionChartComponent {
  data = input<OrderAnalyticsDto | null>(null);
  loading = input<boolean>(false);

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  pieChartType: ChartType = 'doughnut';
  lineChartType: ChartType = 'line';

  pieChartData: ChartConfiguration['data'] = {
    datasets: [],
    labels: [],
  };

  lineChartData: ChartConfiguration['data'] = {
    datasets: [],
    labels: [],
  };

  pieChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'bottom',
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.label || '';
            const value = context.parsed;
            const ordersText = this.translate.instant('pages.dashboard.order_analytics.order_count');
            return `${label}: ${value} ${ordersText}`;
          },
        },
      },
    },
  };

  lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false,
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          precision: 0,
        },
      },
    },
  };

  constructor(private translate: TranslateService) {
    effect(() => {
      const currentData = this.data();
      if (currentData) {
        this.updateChartData(currentData);
      }
    });
  }

  private updateChartData(currentData: OrderAnalyticsDto): void {
    if (!currentData) return;

    if (currentData.statusDistribution) {
      const statusColors: Record<string, string> = {
        Pending: '#fbbf24',
        Confirmed: '#8b5cf6',
        InProgress: '#f59e0b',
        Completed: '#10b981',
        Cancelled: '#ef4444',
      };

      const labels = Object.keys(currentData.statusDistribution);
      const values = Object.values(currentData.statusDistribution);
      const colors = labels.map((label) => statusColors[label] || '#6b7280');

      this.pieChartData = {
        labels,
        datasets: [
          {
            data: values,
            backgroundColor: colors,
            borderWidth: 2,
            borderColor: '#fff',
          },
        ],
      };
    }

    if (currentData.weeklyTrends && currentData.weeklyTrends.length > 0) {
      const labels = currentData.weeklyTrends.map(
        (w) => this.translate.instant('pages.dashboard.order_analytics.week', { number: w.weekNumber })
      );
      const orderCounts = currentData.weeklyTrends.map((w) => w.orderCount);

      this.lineChartData = {
        labels,
        datasets: [
          {
            data: orderCounts,
            label: this.translate.instant('pages.dashboard.order_analytics.chart_label'),
            borderColor: '#0284c7',
            backgroundColor: 'rgba(2, 132, 199, 0.1)',
            fill: true,
            tension: 0.4,
            pointRadius: 4,
            pointBackgroundColor: '#0284c7',
            pointBorderColor: '#fff',
            pointBorderWidth: 2,
          },
        ],
      };
    }

    this.chart?.update();
  }
}
