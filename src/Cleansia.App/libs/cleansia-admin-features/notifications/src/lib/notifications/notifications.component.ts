import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { NotificationsFacade } from './notifications.facade';
import { NotificationRow } from './notifications.models';

@Component({
  selector: 'cleansia-admin-notifications',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    PaginatorModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './notifications.component.html',
  providers: [NotificationsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsComponent implements OnInit {
  protected readonly facade = inject(NotificationsFacade);

  ngOnInit(): void {
    this.facade.load();
  }

  onPageChange(event: PaginatorState): void {
    this.facade.onPageChange(event.first ?? 0);
  }

  onRowKeydown(row: NotificationRow, event: Event): void {
    event.preventDefault();
    this.facade.open(row);
  }
}
