import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CleansiaSectionComponent } from '@cleansia/components';
import { currentLanguage } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { extraLabel } from '../order-details.helpers';

@Component({
  selector: 'cleansia-partner-order-extras',
  standalone: true,
  imports: [CommonModule, CleansiaSectionComponent, TranslatePipe],
  templateUrl: './order-extras.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderExtrasComponent {
  private readonly translate = inject(TranslateService);
  private readonly lang = currentLanguage(this.translate);

  extrasEntries = input<[string, boolean][]>();

  protected readonly labels = computed(() => {
    this.lang();
    return (this.extrasEntries() ?? []).map(([slug]) => ({
      slug,
      label: extraLabel(slug, this.translate),
    }));
  });
}
