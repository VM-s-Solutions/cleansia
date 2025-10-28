import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, effect, ViewChild } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CleansiaLoaderComponent, CleansiaLabelComponent } from '@cleansia/components';
import { EarningsAnalyticsDto } from '@cleansia/services';
import { ChartConfiguration, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';

@Component({
  selector: 'cleansia-earnings-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective, TranslateModule, CleansiaLoaderComponent, CleansiaLabelComponent],
  templateUrl: './cleansia-earnings-chart.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaEarningsChartComponent {
  data = input<EarningsAnalyticsDto | null>(null);
  loading = input<boolean>(false);

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

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
          label: (context) => {
            const label = context.dataset.label || '';
            const value = context.parsed.y;
            const locale = this.translate.currentLang || 'cs-CZ';
            const formatted = value != null ? value.toLocaleString(locale) : '0';
            return `${label}: ${formatted} Kč`;
          },
        },
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          callback: (value) => {
            const locale = this.translate.currentLang || 'cs-CZ';
            return `${value.toLocaleString(locale)} Kč`;
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
          label: 'Monthly Earnings',
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
