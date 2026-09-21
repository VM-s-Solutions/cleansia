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
import { CleansiaLabelComponent } from '@cleansia/components';
import { TimeAnalyticsDto } from '@cleansia/partner-services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartConfiguration, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { Skeleton } from 'primeng/skeleton';
import { formatHours, HOUR_TICK_STEP_MINUTES } from './time-analytics.helpers';

interface ServiceTimeRow {
  readonly serviceName: string;
  readonly orderCount: string;
  readonly totalHours: string;
  readonly averageHours: string;
}

@Component({
  selector: 'cleansia-time-analytics-chart',
  standalone: true,
  imports: [
    CommonModule,
    BaseChartDirective,
    TranslateModule,
    Skeleton,
    CleansiaLabelComponent,
  ],
  templateUrl: './cleansia-time-analytics-chart.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTimeAnalyticsChartComponent {
  private readonly translate = inject(TranslateService);

  data = input<TimeAnalyticsDto | null>(null);
  loading = input<boolean>(false);

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  private readonly lang = currentLanguage(this.translate);

  readonly totalHours = computed(() => formatHours(this.data()?.totalMinutesWorked, this.lang()));
  readonly averageHours = computed(() =>
    formatHours(this.data()?.averageMinutesPerOrder, this.lang())
  );
  readonly efficiency = computed(() => `${Math.round(this.data()?.efficiencyRate ?? 0)}%`);
  readonly totalOrders = computed(() => String(this.data()?.totalOrders ?? 0));

  readonly serviceRows = computed((): ServiceTimeRow[] => {
    const lang = this.lang();
    const unit = this.translate.instant('pages.dashboard.time_analytics.orders').toLowerCase();
    return (this.data()?.byServiceType ?? []).map((service) => ({
      serviceName: service.serviceName ?? '',
      orderCount: `${service.orderCount} ${unit}`,
      totalHours: formatHours(service.totalMinutes, lang),
      averageHours: this.translate.instant('pages.dashboard.time_analytics.avg_label', {
        value: formatHours(service.averageMinutesPerOrder, lang),
      }),
    }));
  });

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
          label: (context) =>
            `${context.dataset.label || ''}: ${formatHours(context.parsed.y, this.lang())}`,
        },
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          stepSize: HOUR_TICK_STEP_MINUTES,
          callback: (value) => formatHours(Number(value), this.lang(), 0),
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
