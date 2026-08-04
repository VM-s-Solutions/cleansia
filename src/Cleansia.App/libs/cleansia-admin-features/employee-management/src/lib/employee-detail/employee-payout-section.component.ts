import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnInit, inject } from '@angular/core';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeePayoutFacade } from './employee-payout.facade';

@Component({
  selector: 'cleansia-employee-payout-section',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './employee-payout-section.component.html',
  providers: [EmployeePayoutFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeePayoutSectionComponent implements OnInit {
  protected readonly facade = inject(EmployeePayoutFacade);
  protected readonly Policy = Policy;

  @Input({ required: true }) employeeId!: string;

  ngOnInit(): void {
    this.facade.load(this.employeeId);
  }

  onRetry(): void {
    this.facade.retry(this.employeeId);
  }

  onReveal(): void {
    this.facade.reveal(this.employeeId);
  }
}
