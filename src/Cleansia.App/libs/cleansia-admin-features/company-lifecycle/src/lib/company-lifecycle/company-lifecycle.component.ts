import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { WindDownDialogComponent } from '../wind-down-dialog/wind-down-dialog.component';
import { CompanyLifecycleFacade } from './company-lifecycle.facade';
import { getSettlementFactsTableDefinition, LifecycleAct, SettlementFactRow } from './company-lifecycle.models';

@Component({
  selector: 'cleansia-admin-company-lifecycle',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaPermissionDirective,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    WindDownDialogComponent,
  ],
  templateUrl: './company-lifecycle.component.html',
  providers: [CompanyLifecycleFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompanyLifecycleComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CompanyLifecycleFacade);
  protected readonly Policy = Policy;
  protected readonly LifecycleAct = LifecycleAct;

  valueTemplate = viewChild<TemplateRef<SettlementFactRow>>('valueTemplate');
  statusTemplate = viewChild<TemplateRef<SettlementFactRow>>('statusTemplate');

  columns!: TableColumn<SettlementFactRow>[];

  private readonly destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.rebuildTableDefinition();
    this.cd.detectChanges();

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinition();
      this.cd.detectChanges();
    });

    this.facade.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTableDefinition(): void {
    this.columns = getSettlementFactsTableDefinition(this.translate, this.valueTemplate(), this.statusTemplate()).columns;
  }
}
