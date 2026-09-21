import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaCheckboxComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { DynamicDialogConfig } from 'primeng/dynamicdialog';
import { Skeleton } from 'primeng/skeleton';
import { formatDateTime } from '../../order-details/order-details.helpers';
import { WorkContractDialogFacade } from './work-contract-dialog.facade';
import { WorkContractDialogData, WorkContractDialogMode } from './work-contract-dialog.models';

@Component({
  selector: 'cleansia-partner-work-contract-dialog',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    Skeleton,
  ],
  templateUrl: './work-contract-dialog.component.html',
  providers: [WorkContractDialogFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkContractDialogComponent implements OnInit {
  protected readonly facade = inject(WorkContractDialogFacade);
  private readonly config = inject<DynamicDialogConfig<WorkContractDialogData>>(DynamicDialogConfig);

  protected readonly acceptanceControl = new FormControl(false, { nonNullable: true });
  protected readonly formatDateTime = (date: string | Date | undefined): string =>
    formatDateTime(date, this.facade.language());

  protected readonly isRead = computed(() => this.facade.mode() === WorkContractDialogMode.Read);

  protected readonly submitLabelKey = computed(() =>
    this.facade.mode() === WorkContractDialogMode.Take
      ? 'pages.orders.work_contract.accept_and_take'
      : 'pages.orders.work_contract.accept_only'
  );

  ngOnInit(): void {
    const data = this.config.data;
    if (!data) {
      this.facade.cancel();
      return;
    }
    this.facade.connectAcceptanceControl(this.acceptanceControl);
    this.facade.load(data);
  }

  protected onSubmit(): void {
    this.facade.submit();
  }

  protected onCancel(): void {
    this.facade.cancel();
  }

  protected onRetry(): void {
    this.facade.retry();
  }
}
