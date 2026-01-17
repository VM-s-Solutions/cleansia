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
import { TimeAnalyticsDto } from '@cleansia/partner-services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartConfiguration, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';

@Component({
  selector: 'cleansia-time-analytics-chart',
  standalone: true,
  imports: [
    CommonModule,
    BaseChartDirective,
    TranslateModule,
    CleansiaLoaderComponent,
    CleansiaLabelComponent,
  ],
  templateUrl: './cleansia-time-analytics-chart.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTimeAnalyticsChartComponent {
  data = input<TimeAnalyticsDto | null>(null);
  loading = input<boolean>(false);

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  barChartType: ChartType = 'bar';
  barChartData: ChartConfiguration['data'] = {
    datasets: [],
    labels: [],
  };

  barChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top',
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.dataset.label || '';
            const value = context.parsed.y;
            return `${label}: ${(value ?? 0 / 60).toFixed(1)} hrs`;
          },
        },
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          callback: (value) => {
            return `${(Number(value) / 60).toFixed(0)} hrs`;
          },
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

  private updateChartData(currentData: TimeAnalyticsDto): void {
    if (
      !currentData?.weeklyBreakdown ||
      currentData.weeklyBreakdown.length === 0
    ) {
      return;
    }

    const labels = currentData.weeklyBreakdown.map(
      (w) => this.translate.instant('pages.dashboard.time_analytics.week', { number: w.weekNumber })
    );
    const timeData = currentData.weeklyBreakdown.map((w) => w.totalMinutes);

    this.barChartData = {
      labels,
      datasets: [
        {
          data: timeData,
          label: this.translate.instant('pages.dashboard.time_analytics.chart_label'),
          backgroundColor: 'rgba(245, 158, 11, 0.6)',
          borderColor: '#f59e0b',
          borderWidth: 2,
          borderRadius: 4,
        },
      ],
    };

    this.chart?.update();
  }
}
