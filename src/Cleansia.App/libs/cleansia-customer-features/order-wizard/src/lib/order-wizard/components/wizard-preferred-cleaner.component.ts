import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CleansiaSelectComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { OrderWizardFacade } from '../order-wizard.facade';

@Component({
  selector: 'cleansia-wizard-preferred-cleaner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, TranslatePipe, CleansiaSelectComponent],
  templateUrl: './wizard-preferred-cleaner.component.html',
})
export class WizardPreferredCleanerComponent {
  @Input({ required: true }) facade!: OrderWizardFacade;

  onSelect(employeeId: string | null): void {
    this.facade.selectPreferredCleaner(employeeId ?? null);
  }
}
