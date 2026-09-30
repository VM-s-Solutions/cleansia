import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CleansiaSectionComponent, CleansiaTitleComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-partner-how-jobs-are-offered',
  standalone: true,
  imports: [TranslatePipe, CleansiaSectionComponent, CleansiaTitleComponent],
  templateUrl: './how-jobs-are-offered.component.html',
  styleUrl: './how-jobs-are-offered.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HowJobsAreOfferedComponent {}
