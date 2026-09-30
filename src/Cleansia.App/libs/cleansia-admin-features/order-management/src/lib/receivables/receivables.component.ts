import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ReceivableListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ReceivablesFacade } from './receivables.facade';
import {
  buildReceivableStatusOptions,
  formatReceivableAmount,
  getReceivablesTableDefinition,
  receivableKindLabel,
} from './receivables.models';

@Component({
  selector: 'cleansia-admin-receivables',
  standalone: true,
  imports: [
    FormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTextareaComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './receivables.component.html',
  providers: [ReceivablesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReceivablesComponent implements OnInit {
  protected readonly facade = inject(ReceivablesFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  protected readonly statusOptions = computed(() => {
    this.facade.lang();
    return buildReceivableStatusOptions(this.translate);
  });

  protected readonly table = computed(() => {
    this.facade.lang();
    return getReceivablesTableDefinition(
      {
        onViewOrder: (row) => this.facade.viewOrder(row),
        onWriteOff: (row) => this.facade.startWriteOff(row),
        isBusy: () => this.facade.submitting(),
      },
      this.translate,
      this.permissions
    );
  });

  ngOnInit(): void {
    this.facade.loadReceivables();
  }

  protected kindOf(row: ReceivableListItem): string {
    return receivableKindLabel(row, this.translate);
  }

  protected amountOf(row: ReceivableListItem): string {
    return formatReceivableAmount(row, this.facade.lang());
  }
}
