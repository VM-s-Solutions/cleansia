import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaFormSkeletonComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileJobRadiusFacade } from '../../profile/profile-job-radius.facade';
import {
  JOB_RADIUS_MAX_KM,
  JOB_RADIUS_MIN_KM,
} from '../../profile/profile-job-radius.models';

@Component({
  selector: 'cleansia-partner-profile-job-radius',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaCheckboxComponent,
    CleansiaTextInputComponent,
    CleansiaButtonComponent,
    CleansiaFormSkeletonComponent,
  ],
  templateUrl: './profile-job-radius.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileJobRadiusComponent {
  @Input({ required: true }) facade!: ProfileJobRadiusFacade;
  @Output() retry = new EventEmitter<void>();

  protected readonly minKm = JOB_RADIUS_MIN_KM;
  protected readonly maxKm = JOB_RADIUS_MAX_KM;
}
