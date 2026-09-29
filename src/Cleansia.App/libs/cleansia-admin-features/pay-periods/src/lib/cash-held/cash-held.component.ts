import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CleanerCashHeldDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { PermissionService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CashHeldFacade } from './cash-held.facade';
import { formatCashHeld, getCashHeldTableDefinition } from './cash-held.models';

@Component({
  selector: 'cleansia-admin-cash-held',
  standalone: true,
  imports: [
    FormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTextareaComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './cash-held.component.html',
  providers: [CashHeldFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CashHeldComponent implements OnInit {
  protected readonly facade = inject(CashHeldFacade);
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  protected readonly table = computed(() => {
    this.facade.lang();
    return getCashHeldTableDefinition(
      {
        onRecordRemittance: (row) => this.facade.startRemittance(row),
        onWriteOff: (row) => this.facade.startWriteOff(row),
        isBusy: () => this.facade.submitting(),
      },
      this.translate,
      this.permissions
    );
  });

  ngOnInit(): void {
    this.facade.loadCashHeld();
  }

  protected heldOf(row: CleanerCashHeldDto): string {
    return formatCashHeld(row, this.facade.lang());
  }
}
