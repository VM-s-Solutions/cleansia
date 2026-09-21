import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CleansiaButtonComponent, CleansiaLoaderComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { DynamicDialogConfig } from 'primeng/dynamicdialog';
import { AdminWorkContractDialogFacade } from './admin-work-contract-dialog.facade';
import { AdminWorkContractDialogData } from './admin-work-contract-dialog.models';

@Component({
  selector: 'cleansia-admin-work-contract-dialog',
  standalone: true,
  imports: [TranslatePipe, CleansiaButtonComponent, CleansiaLoaderComponent],
  templateUrl: './admin-work-contract-dialog.component.html',
  providers: [AdminWorkContractDialogFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminWorkContractDialogComponent implements OnInit {
  protected readonly facade = inject(AdminWorkContractDialogFacade);
  private readonly config = inject<DynamicDialogConfig<AdminWorkContractDialogData>>(DynamicDialogConfig);

  ngOnInit(): void {
    const acceptanceId = this.config.data?.acceptanceId;
    if (!acceptanceId) {
      this.facade.close();
      return;
    }
    this.facade.load(acceptanceId);
  }

  protected onClose(): void {
    this.facade.close();
  }

  protected onRetry(): void {
    this.facade.retry();
  }
}
