import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CleansiaDateRangeSelectorComponent } from '../components/date-range-selector/cleansia-date-range-selector.component';
import { CleansiaEarningsChartComponent } from '../components/earnings-chart/cleansia-earnings-chart.component';
import { CleansiaOrderDistributionChartComponent } from '../components/order-distribution-chart/cleansia-order-distribution-chart.component';
import { CleansiaProductivityGaugesComponent } from '../components/productivity-gauges/cleansia-productivity-gauges.component';
import { CleansiaTimeAnalyticsChartComponent } from '../components/time-analytics-chart/cleansia-time-analytics-chart.component';
import { DashboardFacade } from './dashboard.facade';
import { StatCard } from './dashboard.models';

@Component({
  selector: 'cleansia-partner-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CardModule,
    ButtonModule,
    CommonModule,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaEarningsChartComponent,
    CleansiaTimeAnalyticsChartComponent,
    CleansiaOrderDistributionChartComponent,
    CleansiaProductivityGaugesComponent,
    CleansiaDateRangeSelectorComponent,
  ],
  templateUrl: './dashboard.component.html',
  providers: [DashboardFacade],
})
export class DashboardComponent {
  protected readonly facade = inject(DashboardFacade);

  onCardClick(card: StatCard): void {
    if (card.route) {
      this.facade.navigateTo(card.route);
    }
  }

  onRefresh(): void {
    this.facade.refresh();
  }

  onDateRangeChanged(range: { startDate: Date; endDate: Date }): void {
    this.facade.onDateRangeChanged(range.startDate, range.endDate);
  }

  getTrendIcon(direction: 'up' | 'down' | 'neutral'): string {
    switch (direction) {
      case 'up':
        return 'pi pi-arrow-up';
      case 'down':
        return 'pi pi-arrow-down';
      default:
        return 'pi pi-minus';
    }
  }

  getTrendClass(direction: 'up' | 'down' | 'neutral'): string {
    switch (direction) {
      case 'up':
        return 'trend-up';
      case 'down':
        return 'trend-down';
      default:
        return 'trend-neutral';
    }
  }
}
