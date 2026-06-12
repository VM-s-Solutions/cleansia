import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CleansiaButtonComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { NotificationPreferencesFacade } from './notification-preferences.facade';
import { NOTIFICATION_PREFERENCE_CATEGORIES } from './notification-preferences.models';

@Component({
  selector: 'cleansia-customer-notification-preferences',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    SkeletonModule,
    ToggleSwitchModule,
    CleansiaButtonComponent,
  ],
  templateUrl: './notification-preferences.component.html',
  providers: [NotificationPreferencesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationPreferencesComponent implements OnInit {
  protected readonly facade = inject(NotificationPreferencesFacade);
  protected readonly categories = NOTIFICATION_PREFERENCE_CATEGORIES;

  ngOnInit(): void {
    this.facade.load();
  }
}
